using BotFramework.Contracts.Tenancy;

namespace SampleGame.Infrastructure;

/// <summary>
/// Stable key shape for an atomic module-owned state store. The actual
/// database implementation belongs here and receives only opaque identities.
/// </summary>
public static class SampleGameStateKey
{
    public static string For(TenantContext context) =>
        $"{context.TenantId.Value}:{context.ScopeId.Value}:{context.PlayerId?.Value ?? "anonymous"}";
}
