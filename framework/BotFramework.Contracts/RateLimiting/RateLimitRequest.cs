using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Contracts.RateLimiting;

public sealed record RateLimitRequest(
    TenantId TenantId,
    PlayerId? PlayerId,
    BotChannel Channel,
    string RouteKey,
    string? IpAddress = null);
