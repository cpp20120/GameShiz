using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using BotFramework.Contracts.Observability;
using BotFramework.Contracts.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace BotFramework.Rest.RateLimiting;

/// <summary>
/// Atomic multi-dimensional token bucket. Redis is the coordination plane;
/// local buckets keep the service available during a Redis outage and are
/// bounded to avoid turning an outage into unbounded memory growth.
/// </summary>
public sealed class RedisRateLimiter : IRateLimiter, IDisposable
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
    private readonly IRateLimitPolicyProvider _policyProvider;
    private readonly ILogger<RedisRateLimiter> _logger;
    private readonly ConcurrentDictionary<string, LocalBucket> _local = new(StringComparer.Ordinal);
    private readonly object _localGate = new();
    private readonly object _redisGate = new();
    private ConnectionMultiplexer? _redis;
    private long _nextRedisAttemptTicks;
    private long _lastRedisWarningTicks;

    public RedisRateLimiter(
        IOptions<RateLimitOptions> options,
        ILogger<RedisRateLimiter> logger,
        IRateLimitPolicyProvider? policyProvider = null)
    {
        _options = options.Value;
        _logger = logger;
        _policyProvider = policyProvider ?? new DefaultRateLimitPolicyProvider();
    }

    public async ValueTask<RateLimitDecision> CheckAsync(RateLimitRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRouteKey(request.RouteKey);
        var policies = await _policyProvider.ResolveAsync(
            request,
            _options.Deployment(request.Channel),
            cancellationToken).ConfigureAwait(false);
        var buckets = BuildBuckets(request, policies);

        if (!_options.Enabled)
        {
            BotFrameworkMetrics.SetRateLimitFallback(false);
            return Allowed(buckets[0].Policy, isFallback: false, policies.Version);
        }

        var redis = GetRedis();
        if (redis is not null && redis.IsConnected)
        {
            try
            {
                var decision = await CheckRedisAsync(redis.GetDatabase(), buckets, policies.Version, cancellationToken).ConfigureAwait(false);
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

    private async Task<RateLimitDecision> CheckRedisAsync(
        IDatabase database,
        IReadOnlyList<Bucket> buckets,
        string policyVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var keys = buckets.Select(bucket => (RedisKey)bucket.Key).ToArray();
        var args = new List<RedisValue> { DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), buckets.Count };
        foreach (var configuredBucket in buckets)
        {
            args.Add(configuredBucket.Policy.Capacity);
            args.Add(configuredBucket.Policy.RefillPerSecond);
        }

        var result = (RedisResult[])(await database.ScriptEvaluateAsync(
            TokenBucketScript,
            keys,
            args.ToArray()).ConfigureAwait(false))!;
        var allowed = ParseInt(result[0]) == 1;
        var deniedIndex = Math.Max(0, ParseInt(result[2]) - 1);
        var bucket = buckets[deniedIndex];
        var remaining = Math.Max(0, (int)Math.Floor(ParseDouble(result[3])));
        return new RateLimitDecision(
            allowed,
            allowed ? null : bucket.Dimension,
            bucket.Policy.Capacity,
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
                states[i] = new LocalBucketState(state, bucket.Policy);
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
                return new RateLimitDecision(false, deniedBucket.Dimension, deniedBucket.Policy.Capacity,
                    Math.Max(0, (int)Math.Floor(states[denied].State.Tokens)), retry, true, policyVersion);
            }

            foreach (var state in states)
                state.State.Tokens -= 1;
            return Allowed(buckets[0].Policy, isFallback: true, policyVersion);
        }
    }

    private IReadOnlyList<Bucket> BuildBuckets(RateLimitRequest request, RateLimitPolicySet policies)
    {
        var tenant = Key("tenant", request.TenantId.Value);
        var user = request.PlayerId is { } player
            ? Key("tenant-player", request.TenantId.Value, player.Value)
            : null;
        var route = Key("tenant-route", request.TenantId.Value, request.RouteKey);
        var userRoute = request.PlayerId is { } routePlayer
            ? Key("tenant-player-route", request.TenantId.Value, routePlayer.Value, request.RouteKey)
            : null;
        var buckets = new List<Bucket>
        {
            new(tenant, RateLimitDimension.Tenant, policies.Tenant),
            new(route, RateLimitDimension.TenantRoute, policies.Route),
        };
        if (user is not null)
            buckets.Add(new(user, RateLimitDimension.TenantPlayer, policies.Player));
        if (userRoute is not null)
            buckets.Add(new(userRoute, RateLimitDimension.TenantPlayerRoute, policies.PlayerRoute));
        if (request.Channel == BotFramework.Contracts.Messaging.BotChannel.Rest && !string.IsNullOrWhiteSpace(request.IpAddress))
            buckets.Add(new(Key("tenant-ip", request.TenantId.Value, request.IpAddress!), RateLimitDimension.TenantIp, policies.Ip));
        return buckets;
    }

    private string Key(string dimension, params string[] values)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', values)));
        return $"{_options.RedisKeyPrefix}:{dimension}:{Convert.ToHexString(bytes).ToLowerInvariant()}";
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
        _logger.LogWarning(exception, "BotFramework distributed rate limiter is using bounded local fallback.");
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
            new KeyValuePair<string, object?>("channel", request.Channel.ToString().ToLowerInvariant()),
            new KeyValuePair<string, object?>("dimension", decision.DeniedDimension?.ToString().ToLowerInvariant() ?? "none"),
            new KeyValuePair<string, object?>("outcome", decision.Allowed ? "allowed" : "denied"),
            new KeyValuePair<string, object?>("fallback", decision.IsFallback ? "local" : "redis"));

    private static void ValidateRouteKey(string routeKey)
    {
        if (string.IsNullOrWhiteSpace(routeKey) || routeKey.Length > 256 || routeKey.Any(char.IsWhiteSpace) || routeKey.Contains('/'))
            throw new ArgumentException("Route keys must be stable module/command identifiers, not raw URLs.", nameof(routeKey));
    }

    private static int ParseInt(RedisResult value) => int.TryParse(value.ToString(), out var result) ? result : 0;
    private static double ParseDouble(RedisResult value) => double.TryParse(value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;

    private sealed record Bucket(string Key, RateLimitDimension Dimension, RateLimitPolicy Policy);
    private sealed record LocalBucketState(LocalBucket State, RateLimitPolicy Policy);

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

    public void Dispose()
    {
        lock (_redisGate)
        {
            _redis?.Dispose();
            _redis = null;
        }
    }
}
