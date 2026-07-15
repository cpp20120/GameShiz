// ─────────────────────────────────────────────────────────────────────────────
// Middleware pipeline contracts.
//
// The order middleware registers in is the order it wraps, onion-style:
//
//   request  → [logging] → [metrics] → [auth] → [rate-limit] → [validation] → handler
//   response ← [logging] ← [metrics] ← [auth] ← [rate-limit] ← [validation] ← handler
//
// Each middleware gets next() and decides whether to call it, short-circuit,
// wrap in try/catch, or transform. Familiar pattern from ASP.NET Core.
// Modules can contribute middleware too — but most cross-cutting concerns
// are Host-owned so a new game doesn't accidentally reorder the chain.
// ─────────────────────────────────────────────────────────────────────────────

namespace BotFramework.Sdk.Commands;

using BotFramework.Contracts.Tenancy;
/// <summary>
/// Request-scoped identity + tracing metadata. Propagated through the bus so
/// every middleware sees the same view. Built once per incoming Telegram
/// update (or admin HTTP request) by the Host before any command dispatches.
/// </summary>
public sealed record RequestContext(
    long UserId,
    string CultureCode,
    string TraceId,
    IReadOnlyDictionary<string, string> Tags)
{
    /// <summary>Canonical tenant boundary for SDK 0.9 modules.</summary>
    public TenantContext? TenantContext { get; init; }
}

public static class RequestContextFactory
{
    public static RequestContext FromTenantContext(
        TenantContext tenantContext,
        string cultureCode,
        string traceId,
        IReadOnlyDictionary<string, string>? tags = null) =>
        new(
            tenantContext.PlayerId?.Value is { } player
                && long.TryParse(player, out var legacyPlayer)
                ? legacyPlayer
                : 0,
            cultureCode,
            traceId,
            tags ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            TenantContext = tenantContext,
        };
}

public static class RequestContextTenantExtensions
{
    public static TenantContext RequireTenantContext(this RequestContext context) =>
        context.TenantContext ?? throw new InvalidOperationException(
            "Tenant context is unavailable for this command request.");
}
