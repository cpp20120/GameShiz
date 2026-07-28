using BotFramework.Contracts.Tenancy;

namespace BotFramework.Sdk.Admin.Execution;

public sealed record AdminExecutionEnvelope(
    AdminActor Actor,
    string Action,
    object? AuditDetails = null)
{
    public TenantContext? TenantContext { get; init; }
}
