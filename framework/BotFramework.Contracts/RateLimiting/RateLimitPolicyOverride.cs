using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Contracts.RateLimiting;

public sealed record RateLimitPolicyOverride(
    TenantId TenantId,
    BotChannel? Channel,
    string? RouteKey,
    RateLimitDimension Dimension,
    RateLimitPolicy Policy);
