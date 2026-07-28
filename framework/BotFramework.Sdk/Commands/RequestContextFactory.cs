using System.Globalization;
using BotFramework.Contracts.Tenancy;

namespace BotFramework.Sdk.Commands;

public static class RequestContextFactory
{
    public static RequestContext FromTenantContext(
        TenantContext tenantContext,
        string cultureCode,
        string traceId,
        IReadOnlyDictionary<string, string>? tags = null) =>
        new(
            tenantContext.PlayerId?.Value is { } player
            && long.TryParse(player, CultureInfo.InvariantCulture, out var legacyPlayer)
                ? legacyPlayer
                : 0,
            cultureCode,
            traceId,
            tags ?? new Dictionary<string, string>(StringComparer.Ordinal))
        {
            TenantContext = tenantContext,
        };
}
