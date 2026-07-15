using BotFramework.Contracts.Tenancy;

namespace BotFramework.Host.Execution;

public sealed record GameExecutionEnvelope<TCommand>(TCommand Command)
{
    public TenantContext? TenantContext { get; init; }
}
