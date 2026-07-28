using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

public abstract class AtomicEffectHandler<TEffect> : IAtomicEffectHandler
    where TEffect : class, IAtomicEffect
{
    public Type EffectType => typeof(TEffect);

    public Task ApplyAsync(IAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct) =>
        ApplyAsync((TEffect)effect, context, ct);

    protected abstract Task ApplyAsync(TEffect effect, IAtomicEffectContext context, CancellationToken ct);
}