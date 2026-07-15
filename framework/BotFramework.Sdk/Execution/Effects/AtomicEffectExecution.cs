namespace BotFramework.Sdk.Execution;

using BotFramework.Contracts.Tenancy;

/// <summary>
/// A mutation that is applied by the Host inside one transaction. Unlike a
/// game decision, an atomic effect is useful for workflows that do not have a
/// user-facing pure action (claims, tournaments and scheduled settlement).
/// </summary>
public interface IAtomicEffect;

public sealed record AtomicEffectExecutionEnvelope(
    string GameId,
    string CommandId,
    string AggregateId,
    IReadOnlyList<string> LockKeys)
{
    public TenantContext? TenantContext { get; init; }
}

public sealed record AtomicEffectPlan<TResult>(
    TResult Result,
    IReadOnlyList<IAtomicEffect> Effects,
    Func<IReadOnlyDictionary<string, object?>, TResult>? ResultFactory = null);
