namespace BotFramework.Contracts.RateLimiting;

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
