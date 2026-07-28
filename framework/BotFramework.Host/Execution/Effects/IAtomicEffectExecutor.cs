using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

public interface IAtomicEffectExecutor
{
    Task<TResult> ExecuteAsync<TResult>(
        AtomicEffectExecutionEnvelope envelope,
        AtomicEffectPlan<TResult> plan,
        CancellationToken ct);
}