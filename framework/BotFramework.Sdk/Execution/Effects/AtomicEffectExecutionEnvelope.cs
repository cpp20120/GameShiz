using BotFramework.Contracts.Tenancy;

namespace BotFramework.Sdk.Execution;

public sealed record AtomicEffectExecutionEnvelope(
    string GameId,
    string CommandId,
    string AggregateId,
    IReadOnlyList<string> LockKeys)
{
    public TenantContext? TenantContext { get; init; }
}