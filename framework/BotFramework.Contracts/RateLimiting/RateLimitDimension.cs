namespace BotFramework.Contracts.RateLimiting;

public enum RateLimitDimension
{
    Tenant = 0,
    TenantPlayer = 1,
    TenantIp = 2,
    TenantRoute = 3,
    TenantPlayerRoute = 4,
}
