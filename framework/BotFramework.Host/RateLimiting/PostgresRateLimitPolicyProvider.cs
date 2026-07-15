using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Observability;
using BotFramework.Contracts.RateLimiting;
using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Persistence.Connections;
using Dapper;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace BotFramework.Host.RateLimiting;

/// <summary>
/// Loads tenant rate-limit overrides from PostgreSQL and caches the complete
/// override set for one tenant in Redis. Writes invalidate the Redis and local
/// entries; the monotonically increasing database version is included in the
/// decision metadata so all replicas expose the same effective policy version.
/// </summary>
public sealed class PostgresRateLimitPolicyProvider(
    INpgsqlConnectionFactory connections,
    ILogger<PostgresRateLimitPolicyProvider> logger,
    IConnectionMultiplexer? redis = null)
    : IRateLimitPolicyProvider, IRateLimitPolicyAdmin
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan LocalCacheTtl = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RedisCacheTtl = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, CacheEntry> localCache = new(StringComparer.Ordinal);

    public async ValueTask<RateLimitPolicySet> ResolveAsync(
        RateLimitRequest request,
        RateLimitPolicySet deployment,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(deployment);

        var tenantKey = CacheKey(request.TenantId);
        if (localCache.TryGetValue(tenantKey, out var local)
            && local.ExpiresAt > DateTimeOffset.UtcNow)
        {
            BotFrameworkMetrics.RateLimitPolicyCacheHits.Add(1, CacheLabels("memory"));
            return Apply(local.Payload, request, deployment);
        }

        var payload = await TryLoadCachedAsync(tenantKey, cancellationToken).ConfigureAwait(false);
        if (payload is null)
        {
            BotFrameworkMetrics.RateLimitPolicyCacheMisses.Add(1, CacheLabels("miss"));
            payload = await LoadFromPostgresAsync(request.TenantId, cancellationToken).ConfigureAwait(false);
            if (payload is not null)
                await TryStoreCachedAsync(tenantKey, payload, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            BotFrameworkMetrics.RateLimitPolicyCacheHits.Add(1, CacheLabels("redis"));
        }

        if (payload is null)
            return deployment;

        localCache[tenantKey] = new(payload, DateTimeOffset.UtcNow + LocalCacheTtl);
        return Apply(payload, request, deployment);
    }

    public async Task UpsertAsync(
        RateLimitPolicyOverride policy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(policy);
        Validate(policy);

        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var tenantKey = await connection.QuerySingleOrDefaultAsync<long?>(new CommandDefinition(
            "SELECT tenant_key FROM tenants WHERE tenant_id = @tenantId",
            new { tenantId = policy.TenantId.Value },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        if (tenantKey is null)
            throw new InvalidOperationException($"Tenant '{policy.TenantId}' is not provisioned.");

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO rate_limit_policy_overrides (
                tenant_key, channel, route_key, dimension, capacity, refill_per_second, version)
            VALUES (@tenantKey, @channel, @routeKey, @dimension, @capacity, @refill, 1)
            ON CONFLICT (tenant_key, channel, route_key, dimension)
            DO UPDATE SET
                capacity = EXCLUDED.capacity,
                refill_per_second = EXCLUDED.refill_per_second,
                version = rate_limit_policy_overrides.version + 1,
                updated_at = now()
            """,
            new
            {
                tenantKey,
                channel = policy.Channel?.ToString().ToLowerInvariant() ?? string.Empty,
                routeKey = policy.RouteKey ?? string.Empty,
                dimension = policy.Dimension.ToString().ToLowerInvariant(),
                capacity = policy.Policy.Capacity,
                refill = policy.Policy.RefillPerSecond,
            },
            transaction,
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        await InvalidateAsync(policy.TenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task RemoveAsync(
        TenantId tenantId,
        BotChannel? channel,
        string? routeKey,
        RateLimitDimension dimension,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId.Value))
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        ValidateRoute(routeKey);

        await using var connection = await connections.OpenAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            DELETE FROM rate_limit_policy_overrides o
            USING tenants t
            WHERE o.tenant_key = t.tenant_key
              AND t.tenant_id = @tenantId
              AND o.channel = @channel
              AND o.route_key = @routeKey
              AND o.dimension = @dimension
            """,
            new
            {
                tenantId = tenantId.Value,
                channel = channel?.ToString().ToLowerInvariant() ?? string.Empty,
                routeKey = routeKey ?? string.Empty,
                dimension = dimension.ToString().ToLowerInvariant(),
            },
            cancellationToken: cancellationToken)).ConfigureAwait(false);
        await InvalidateAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    public async Task InvalidateAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(tenantId);
        localCache.TryRemove(key, out _);
        if (redis is not null)
            await redis.GetDatabase().KeyDeleteAsync(RedisKey(key)).ConfigureAwait(false);
        BotFrameworkMetrics.RateLimitPolicyCacheInvalidations.Add(1, CacheLabels("write"));
        await Task.CompletedTask.ConfigureAwait(false);
    }

    private async Task<PolicyPayload?> LoadFromPostgresAsync(TenantId tenantId, CancellationToken ct)
    {
        try
        {
            await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
            var rows = (await connection.QueryAsync<PolicyRow>(new CommandDefinition(
                """
                SELECT o.channel AS Channel,
                       o.route_key AS RouteKey,
                       o.dimension AS Dimension,
                       o.capacity AS Capacity,
                       o.refill_per_second AS RefillPerSecond,
                       o.version AS Version
                FROM rate_limit_policy_overrides o
                JOIN tenants t ON t.tenant_key = o.tenant_key
                WHERE t.tenant_id = @tenantId
                """,
                new { tenantId = tenantId.Value },
                cancellationToken: ct))).ToArray();
            return new PolicyPayload(
                rows,
                rows.Length == 0 ? 0 : rows.Max(row => row.Version));
        }
        catch (Npgsql.NpgsqlException exception)
        {
            logger.LogWarning(exception, "Unable to load rate-limit overrides for tenant {TenantId}; deployment defaults remain active.", tenantId.Value);
            return null;
        }
    }

    private async Task<PolicyPayload?> TryLoadCachedAsync(string tenantKey, CancellationToken ct)
    {
        if (redis is null)
            return null;

        try
        {
            var value = await redis.GetDatabase().StringGetAsync(RedisKey(tenantKey)).ConfigureAwait(false);
            return value.HasValue ? JsonSerializer.Deserialize<PolicyPayload>(value.ToString(), JsonOptions) : null;
        }
        catch (Exception exception) when (exception is RedisException or IOException or JsonException or NotSupportedException)
        {
            logger.LogWarning(exception, "Unable to read rate-limit policy cache; PostgreSQL will be queried.");
            return null;
        }
    }

    private async Task TryStoreCachedAsync(string tenantKey, PolicyPayload payload, CancellationToken ct)
    {
        if (redis is null)
            return;

        try
        {
            var serialized = JsonSerializer.Serialize(payload, JsonOptions);
            await redis.GetDatabase().StringSetAsync(RedisKey(tenantKey), serialized, RedisCacheTtl).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is RedisException or IOException)
        {
            logger.LogWarning(exception, "Unable to write rate-limit policy cache; continuing without Redis policy cache.");
        }
    }

    private static RateLimitPolicySet Apply(
        PolicyPayload payload,
        RateLimitRequest request,
        RateLimitPolicySet deployment)
    {
        var version = payload.Version == 0
            ? deployment.Version
            : $"{deployment.Version}:tenant-{payload.Version}";
        var result = deployment with { Version = version };
        result = result with
        {
            Tenant = Resolve(RateLimitDimension.Tenant, result.Tenant),
            Player = Resolve(RateLimitDimension.TenantPlayer, result.Player),
            Ip = Resolve(RateLimitDimension.TenantIp, result.Ip),
            Route = Resolve(RateLimitDimension.TenantRoute, result.Route),
            PlayerRoute = Resolve(RateLimitDimension.TenantPlayerRoute, result.PlayerRoute),
        };
        return result;

        RateLimitPolicy Resolve(RateLimitDimension dimension, RateLimitPolicy fallback)
        {
            var rows = payload.Rows
                .Where(row => string.Equals(row.Dimension, dimension.ToString(), StringComparison.OrdinalIgnoreCase))
                .Select(row => (Row: row, Score: Score(row, request)))
                .Where(candidate => candidate.Score > 0)
                .OrderByDescending(candidate => candidate.Score)
                .Select(candidate => candidate.Row)
                .FirstOrDefault();
            return rows is null ? fallback : new RateLimitPolicy(rows.Capacity, rows.RefillPerSecond);
        }
    }

    private static int Score(PolicyRow row, RateLimitRequest request)
    {
        var channelExact = string.Equals(
            row.Channel,
            request.Channel.ToString(),
            StringComparison.OrdinalIgnoreCase);
        var routeExact = string.Equals(row.RouteKey, request.RouteKey, StringComparison.Ordinal);
        if (!string.IsNullOrEmpty(row.RouteKey) && !routeExact)
            return 0;
        if (!string.IsNullOrEmpty(row.Channel)
            && !channelExact)
            return 0;

        return (routeExact ? 2 : 0) + (channelExact ? 1 : 0) + 1;
    }

    private static void Validate(RateLimitPolicyOverride policy)
    {
        if (policy.Policy.Capacity <= 0 || policy.Policy.RefillPerSecond < 0)
            throw new ArgumentOutOfRangeException(nameof(policy), "Rate-limit capacity must be positive and refill must not be negative.");
        ValidateRoute(policy.RouteKey);
    }

    private static void ValidateRoute(string? routeKey)
    {
        if (routeKey is null)
            return;
        if (routeKey.Length > 256 || routeKey.Any(char.IsWhiteSpace) || routeKey.Contains('/'))
            throw new ArgumentException("Route keys must be stable module/command identifiers.", nameof(routeKey));
    }

    private static string CacheKey(TenantId tenantId) => $"tenant:{tenantId.Value}";

    private static RedisKey RedisKey(string tenantKey)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(tenantKey))).ToLowerInvariant();
        return $"botframework:rate-policy:v1:{hash}";
    }

    private static KeyValuePair<string, object?>[] CacheLabels(string result) =>
        [new("result", result)];

    private sealed record CacheEntry(PolicyPayload Payload, DateTimeOffset ExpiresAt);

    // These are deliberately mutable DTOs. Redis policy cache data is an
    // implementation detail, but it must remain deserializable when the
    // provider is running from a trimmed/AOT-friendly host.
    private sealed class PolicyPayload
    {
        public PolicyPayload()
        {
        }

        public PolicyPayload(PolicyRow[] rows, long version)
        {
            Rows = rows;
            Version = version;
        }

        public PolicyRow[] Rows { get; set; } = [];
        public long Version { get; set; }
    }

    private sealed class PolicyRow
    {
        public string Channel { get; set; } = string.Empty;
        public string RouteKey { get; set; } = string.Empty;
        public string Dimension { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public double RefillPerSecond { get; set; }
        public long Version { get; set; }
    }
}
