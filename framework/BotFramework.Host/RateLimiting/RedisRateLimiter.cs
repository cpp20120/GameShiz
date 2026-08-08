using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Observability;
using BotFramework.Contracts.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BotFramework.Host.RateLimiting;

/// <summary>
/// Shared atomic multi-dimensional limiter for non-REST transports and the
/// command pipeline. Redis is the coordination plane; bounded local buckets
/// keep the process available during a Redis outage.
/// </summary>
public sealed partial class RedisRateLimiter : IRateLimiter, IDisposable
{
    private const string TokenBucketScript = """
        local now = tonumber(ARGV[1])
        local count = tonumber(ARGV[2])
        local decisions = {}
        local retry = 0
        local denied = 0
        local index = 3
        for i = 1, count do
          local capacity = tonumber(ARGV[index])
          local refill = tonumber(ARGV[index + 1])
          local state = redis.call('HMGET', KEYS[i], 'tokens', 'updated')
          local tokens = tonumber(state[1])
          local updated = tonumber(state[2])
          if tokens == nil then tokens = capacity end
          if updated == nil then updated = now end
          tokens = math.min(capacity, tokens + math.max(0, now - updated) / 1000 * refill)
          decisions[i] = tokens
          if tokens < 1 and denied == 0 then
            denied = i
            if refill > 0 then retry = math.ceil((1 - tokens) / refill) end
          end
          index = index + 2
        end
        if denied ~= 0 then return { 0, retry, denied, decisions[denied] } end
        index = 3
        for i = 1, count do
          local capacity = tonumber(ARGV[index])
          local refill = tonumber(ARGV[index + 1])
          redis.call('HSET', KEYS[i], 'tokens', decisions[i] - 1, 'updated', now)
          redis.call('PEXPIRE', KEYS[i], math.ceil(1000 * math.max(60, capacity / math.max(refill, 0.001) * 2)) )
          index = index + 2
        end
        return { 1, 0, 0, decisions[1] - 1 }
        """;

    private readonly RateLimitOptions _options;
    private readonly RateLimitPolicySet _restDeployment;
    private readonly IRateLimitPolicyProvider _policyProvider;
    private readonly ILogger<RedisRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, LocalBucket> _local = new(StringComparer.Ordinal);
    private readonly System.Threading.Lock _localGate = new();
    private readonly System.Threading.Lock _redisGate = new();
    private ConnectionMultiplexer? _redis;
    private long _nextRedisAttemptTicks;
    private long _lastRedisWarningTicks;

    public RedisRateLimiter(
        IOptions<RateLimitOptions> options,
        ILogger<RedisRateLimiter> logger,
        IRateLimitPolicyProvider? policyProvider = null)
    {
        _options = options.Value;
        _restDeployment = _options.Deployment(BotChannel.Rest);
        _logger = logger;
        _policyProvider = policyProvider ?? new DefaultRateLimitPolicyProvider();
    }

    public async ValueTask<RateLimitDecision> CheckAsync(
        RateLimitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRouteKey(request.RouteKey);
        var policies = await _policyProvider.ResolveAsync(
            request,
            request.Channel == BotChannel.Rest
                ? _restDeployment
                : _options.Deployment(request.Channel),
            cancellationToken);

        if (!_options.Enabled)
            return Allowed(policies.Tenant, isFallback: false, policies.Version);

        var buckets = BuildBuckets(request, policies);
        var redis = GetRedis();
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var decision = await CheckRedisAsync(redis.GetDatabase(), buckets, policies.Version, cancellationToken);
                BotFrameworkMetrics.SetRateLimitFallback(false);
                RecordDecision(request, decision);
                return decision;
            }
            catch (Exception exception) when (exception is RedisException or RedisTimeoutException or IOException)
            {
                LogRedisFailure(exception);
            }
        }

        BotFrameworkMetrics.SetRateLimitFallback(true);
        var fallbackDecision = CheckLocal(buckets, policies.Version);
        RecordDecision(request, fallbackDecision);
        return fallbackDecision;
    }

    private static async Task<RateLimitDecision> CheckRedisAsync(
        IDatabase database,
        IReadOnlyList<Bucket> buckets,
        string policyVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keys = new RedisKey[buckets.Count];
        var args = new RedisValue[2 + (buckets.Count * 2)];
        args[0] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        args[1] = buckets.Count;
        for (var index = 0; index < buckets.Count; index++)
        {
            var bucket = buckets[index];
            keys[index] = bucket.Key;
            args[2 + (index * 2)] = bucket.Policy.Capacity;
            args[3 + (index * 2)] = bucket.Policy.RefillPerSecond;
        }

        var result = (RedisResult[]?)(await database.ScriptEvaluateAsync(
            TokenBucketScript,
            keys,
            args))
            ?? throw new InvalidOperationException("Redis rate-limit script returned no result.");
        var allowed = ParseInt(result[0]) == 1;
        var deniedIndex = Math.Clamp(ParseInt(result[2]) - 1, 0, buckets.Count - 1);
        var selectedBucket = buckets[deniedIndex];
        var remaining = Math.Max(0, (int)Math.Floor(ParseDouble(result[3])));
        return new RateLimitDecision(
            allowed,
            allowed ? null : selectedBucket.Dimension,
            selectedBucket.Policy.Capacity,
            remaining,
            TimeSpan.FromSeconds(Math.Max(0, ParseInt(result[1]))),
            IsFallback: false,
            policyVersion);
    }

    private RateLimitDecision CheckLocal(IReadOnlyList<Bucket> buckets, string policyVersion)
    {
        lock (_localGate)
        {
            if (_local.Count > _options.LocalMaxKeys)
                RemoveExpiredLocalBuckets();

            var now = DateTimeOffset.UtcNow;
            var states = new LocalBucketState[buckets.Count];
            var denied = -1;
            var retry = TimeSpan.Zero;
            for (var i = 0; i < buckets.Count; i++)
            {
                var bucket = buckets[i];
                EnsureLocalCapacity(bucket.Key);
                var state = _local.GetOrAdd(bucket.Key, _ => new LocalBucket(bucket.Policy.Capacity, now));
                state.Refill(bucket.Policy, now);
                states[i] = new LocalBucketState(state);
                if (denied < 0 && state.Tokens < 1)
                {
                    denied = i;
                    retry = bucket.Policy.RefillPerSecond <= 0
                        ? TimeSpan.MaxValue
                        : TimeSpan.FromSeconds((1 - state.Tokens) / bucket.Policy.RefillPerSecond);
                }
            }

            if (denied >= 0)
            {
                var deniedBucket = buckets[denied];
                return new RateLimitDecision(
                    false,
                    deniedBucket.Dimension,
                    deniedBucket.Policy.Capacity,
                    Math.Max(0, (int)Math.Floor(states[denied].State.Tokens)),
                    retry,
                    IsFallback: true,
                    policyVersion);
            }

            foreach (var state in states)
                state.State.Tokens -= 1;
            return Allowed(buckets[0].Policy, isFallback: true, policyVersion);
        }
    }

    private Bucket[] BuildBuckets(RateLimitRequest request, RateLimitPolicySet policies)
    {
        var tenant = Key("tenant", request.TenantId.Value);
        var route = Key("tenant-route", request.TenantId.Value, request.RouteKey);
        var hasPlayer = request.PlayerId is not null;
        var hasIp = request.Channel == BotChannel.Rest && !string.IsNullOrWhiteSpace(request.IpAddress);
        var buckets = new Bucket[2 + (hasPlayer ? 2 : 0) + (hasIp ? 1 : 0)];
        var index = 0;
        buckets[index++] = new(tenant, RateLimitDimension.Tenant, policies.Tenant);
        buckets[index++] = new(route, RateLimitDimension.TenantRoute, policies.Route);

        if (request.PlayerId is { } player)
        {
            var playerKey = Key("tenant-player", request.TenantId.Value, player.Value);
            var playerRouteKey = Key("tenant-player-route", request.TenantId.Value, player.Value, request.RouteKey);
            buckets[index++] = new(playerKey, RateLimitDimension.TenantPlayer, policies.Player);
            buckets[index++] = new(playerRouteKey, RateLimitDimension.TenantPlayerRoute, policies.PlayerRoute);
        }

        if (request.Channel == BotChannel.Rest && !string.IsNullOrWhiteSpace(request.IpAddress))
            buckets[index] = new(
                Key("tenant-ip", request.TenantId.Value, request.IpAddress),
                RateLimitDimension.TenantIp,
                policies.Ip);

        return buckets;
    }

    private string Key(string dimension, string value1) =>
        HashKey(dimension, ComposeKeyInput(dimension, value1));

    private string Key(string dimension, string value1, string value2) =>
        HashKey(dimension, ComposeKeyInput(dimension, value1, value2));

    private string Key(string dimension, string value1, string value2, string value3) =>
        HashKey(dimension, ComposeKeyInput(dimension, value1, value2, value3));

    private string HashKey(string dimension, string input)
    {
        var byteCount = Encoding.UTF8.GetByteCount(input);
        if (byteCount <= 1024)
        {
            Span<byte> utf8 = stackalloc byte[byteCount];
            return HashKey(dimension, input, utf8);
        }

        var utf8Bytes = Encoding.UTF8.GetBytes(input);
        return HashKey(dimension, input, utf8Bytes);
    }

    private string HashKey(string dimension, string input, Span<byte> utf8)
    {
        Encoding.UTF8.GetBytes(input, utf8);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(utf8, hash);
        return $"{_options.RedisKeyPrefix}:{dimension}:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static string ComposeKeyInput(
        string dimension,
        string value1,
        string? value2 = null,
        string? value3 = null)
    {
        var length = dimension.Length + 1 + value1.Length;
        if (value2 is not null)
            length += 1 + value2.Length;
        if (value3 is not null)
            length += 1 + value3.Length;

        return string.Create(length, (dimension, value1, value2, value3), static (destination, state) =>
        {
            var offset = 0;
            state.dimension.AsSpan().CopyTo(destination[offset..]);
            offset += state.dimension.Length;
            destination[offset++] = '\u001f';
            state.value1.AsSpan().CopyTo(destination[offset..]);
            offset += state.value1.Length;
            if (state.value2 is not { } second)
                return;

            destination[offset++] = '\u001f';
            second.AsSpan().CopyTo(destination[offset..]);
            offset += second.Length;
            if (state.value3 is not { } third)
                return;

            destination[offset++] = '\u001f';
            third.AsSpan().CopyTo(destination[offset..]);
        });
    }

    private ConnectionMultiplexer? GetRedis()
    {
        var current = Volatile.Read(ref _redis);
        if (current is not null && current.IsConnected)
            return current;

        if (DateTime.UtcNow.Ticks < Volatile.Read(ref _nextRedisAttemptTicks))
            return current;

        lock (_redisGate)
        {
            current = _redis;
            if (current is not null && current.IsConnected)
                return current;

            if (DateTime.UtcNow.Ticks < _nextRedisAttemptTicks)
                return current;

            _nextRedisAttemptTicks = DateTime.UtcNow.AddSeconds(5).Ticks;
            if (string.IsNullOrWhiteSpace(_options.RedisConnectionString))
                return null;

            try
            {
                var configuration = ConfigurationOptions.Parse(_options.RedisConnectionString);
                configuration.AbortOnConnectFail = false;
                configuration.ConnectRetry = 1;
                configuration.ConnectTimeout = Math.Min(configuration.ConnectTimeout, 1_000);
                current = ConnectionMultiplexer.Connect(configuration);
                _redis = current;
                return current;
            }
            catch (Exception exception) when (exception is RedisException or IOException)
            {
                LogRedisFailure(exception);
                return null;
            }
        }
    }

    private void LogRedisFailure(Exception exception)
    {
        var now = DateTime.UtcNow.Ticks;
        if (now - Interlocked.Read(ref _lastRedisWarningTicks) < TimeSpan.TicksPerMinute)
            return;
        Interlocked.Exchange(ref _lastRedisWarningTicks, now);
        LogRedisFallback(exception);
    }

    private void RemoveExpiredLocalBuckets()
    {
        foreach (var pair in _local)
        {
            if (DateTimeOffset.UtcNow - pair.Value.UpdatedAt > TimeSpan.FromMinutes(10))
                _local.TryRemove(pair.Key, out _);
        }
    }

    private void EnsureLocalCapacity(string key)
    {
        if (_local.ContainsKey(key))
            return;

        RemoveExpiredLocalBuckets();
        if (_local.Count < _options.LocalMaxKeys)
            return;

        var oldest = _local.MinBy(pair => pair.Value.UpdatedAt);
        if (oldest.Key is not null)
            _local.TryRemove(oldest.Key, out _);
    }

    private static RateLimitDecision Allowed(RateLimitPolicy policy, bool isFallback, string policyVersion) =>
        new(true, null, policy.Capacity, Math.Max(0, policy.Capacity - 1), TimeSpan.Zero, isFallback, policyVersion);

    private static void RecordDecision(RateLimitRequest request, RateLimitDecision decision) =>
        BotFrameworkMetrics.RateLimitDecisions.Add(
            1,
            new KeyValuePair<string, object?>("channel", ChannelLabel(request.Channel)),
            new KeyValuePair<string, object?>("dimension", DimensionLabel(decision.DeniedDimension)),
            new KeyValuePair<string, object?>("outcome", decision.Allowed ? "allowed" : "denied"),
            new KeyValuePair<string, object?>("fallback", decision.IsFallback ? "local" : "redis"));

    private static void ValidateRouteKey(string routeKey)
    {
        if (string.IsNullOrWhiteSpace(routeKey)
            || routeKey.Length > 256
            || routeKey.Any(char.IsWhiteSpace)
            || routeKey.Contains('/'))
            throw new ArgumentException("Route keys must be stable module/command identifiers, not raw URLs.", nameof(routeKey));
    }

    [LoggerMessage(LogLevel.Warning, "BotFramework distributed rate limiter is using bounded local fallback.")]
    private partial void LogRedisFallback(Exception exception);

    private static int ParseInt(RedisResult value) =>
        int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;

    private static double ParseDouble(RedisResult value) =>
        double.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : 0;

    public void Dispose()
    {
        lock (_redisGate)
        {
            _redis?.Dispose();
            _redis = null;
        }
    }

    private static string ChannelLabel(BotChannel channel) => channel switch
    {
        BotChannel.Telegram => "telegram",
        BotChannel.Discord => "discord",
        BotChannel.Rest => "rest",
        BotChannel.System => "system",
        _ => "unknown",
    };

    private static string DimensionLabel(RateLimitDimension? dimension) => dimension switch
    {
        RateLimitDimension.Tenant => "tenant",
        RateLimitDimension.TenantPlayer => "tenant-player",
        RateLimitDimension.TenantIp => "tenant-ip",
        RateLimitDimension.TenantRoute => "tenant-route",
        RateLimitDimension.TenantPlayerRoute => "tenant-player-route",
        _ => "none",
    };

    private readonly record struct Bucket(string Key, RateLimitDimension Dimension, RateLimitPolicy Policy);
    private sealed record LocalBucketState(LocalBucket State);

    private sealed class LocalBucket(double tokens, DateTimeOffset updatedAt)
    {
        public double Tokens { get; set; } = tokens;
        public DateTimeOffset UpdatedAt { get; private set; } = updatedAt;

        public void Refill(RateLimitPolicy policy, DateTimeOffset now)
        {
            Tokens = Math.Min(policy.Capacity, Tokens + Math.Max(0, (now - UpdatedAt).TotalSeconds) * policy.RefillPerSecond);
            UpdatedAt = now;
        }
    }
}
