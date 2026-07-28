using BotFramework.Contracts.Tenancy;

namespace BotFramework.Client;

public sealed record BotFrameworkTenantContext(
    TenantId TenantId,
    ScopeId ScopeId,
    PlayerId? PlayerId = null);
