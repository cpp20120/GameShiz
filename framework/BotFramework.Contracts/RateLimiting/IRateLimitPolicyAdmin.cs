using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Contracts.RateLimiting;

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
