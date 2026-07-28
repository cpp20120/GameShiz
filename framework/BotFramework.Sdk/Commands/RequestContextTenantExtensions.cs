using BotFramework.Contracts.Tenancy;

namespace BotFramework.Sdk.Commands;

public static class RequestContextTenantExtensions
{
    public static TenantContext RequireTenantContext(this RequestContext context) =>
        context.TenantContext ?? throw new InvalidOperationException(
            "Tenant context is unavailable for this command request.");
}