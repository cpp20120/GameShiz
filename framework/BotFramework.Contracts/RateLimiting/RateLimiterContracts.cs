using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Contracts.RateLimiting;

public enum RateLimitDimension
{
    Tenant = 0,
    TenantPlayer = 1,
    TenantIp = 2,
    TenantRoute = 3,
    TenantPlayerRoute = 4,
}

public sealed record RateLimitRequest(
    TenantId TenantId,
    PlayerId? PlayerId,
    BotChannel Channel,
    string RouteKey,
    string? IpAddress = null);

public sealed record RateLimitPolicy(
    int Capacity,
    double RefillPerSecond)
{
    public static RateLimitPolicy PerMinute(int permits) => new(permits, permits / 60d);

    public static RateLimitPolicy PerWindow(int permits, TimeSpan window) =>
        new(permits, permits / window.TotalSeconds);
}

public sealed record RateLimitPolicyOverride(
    TenantId TenantId,
    BotChannel? Channel,
    string? RouteKey,
    RateLimitDimension Dimension,
    RateLimitPolicy Policy);

public interface IRateLimitPolicyAdmin
{
    Task UpsertAsync(RateLimitPolicyOverride policy, CancellationToken cancellationToken = default);
    Task RemoveAsync(
        TenantId tenantId,
        BotChannel? channel,
        string? routeKey,
        RateLimitDimension dimension,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The policies applied to one request. A provider may replace individual
/// dimensions with tenant/route overrides while leaving deployment defaults
/// intact.
/// </summary>
public sealed record RateLimitPolicySet(
    RateLimitPolicy Tenant,
    RateLimitPolicy Player,
    RateLimitPolicy Ip,
    RateLimitPolicy Route,
    RateLimitPolicy PlayerRoute,
    string Version)
{
    public RateLimitPolicy For(RateLimitDimension dimension) => dimension switch
    {
        RateLimitDimension.Tenant => Tenant,
        RateLimitDimension.TenantPlayer => Player,
        RateLimitDimension.TenantIp => Ip,
        RateLimitDimension.TenantRoute => Route,
        RateLimitDimension.TenantPlayerRoute => PlayerRoute,
        _ => throw new ArgumentOutOfRangeException(nameof(dimension), dimension, "Unknown rate-limit dimension."),
    };
}

/// <summary>
/// Resolves deployment defaults plus tenant/route overrides. Implementations
/// must not use raw URLs or player identifiers as policy keys.
/// </summary>
public interface IRateLimitPolicyProvider
{
    ValueTask<RateLimitPolicySet> ResolveAsync(
        RateLimitRequest request,
        RateLimitPolicySet deployment,
        CancellationToken cancellationToken = default);
}

public sealed class DefaultRateLimitPolicyProvider : IRateLimitPolicyProvider
{
    public ValueTask<RateLimitPolicySet> ResolveAsync(
        RateLimitRequest request,
        RateLimitPolicySet deployment,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(deployment);
}

public sealed record RateLimitDecision(
    bool Allowed,
    RateLimitDimension? DeniedDimension,
    int Limit,
    int Remaining,
    TimeSpan RetryAfter,
    bool IsFallback,
    string PolicyVersion);

/// <summary>
/// Request-scoped guard that prevents a transport lease and a nested command
/// lease from charging the same inbound operation twice.
/// </summary>
public sealed class RateLimitRequestState
{
    public bool LeaseGranted { get; set; }
}

/// <summary>
/// Shared limiter boundary used by HTTP, Telegram, Discord, and command
/// pipelines. Implementations must evaluate all applicable dimensions before
/// applying a lease so a rejected request has no game side effects.
/// </summary>
public interface IRateLimiter
{
    ValueTask<RateLimitDecision> CheckAsync(RateLimitRequest request, CancellationToken cancellationToken = default);
}
