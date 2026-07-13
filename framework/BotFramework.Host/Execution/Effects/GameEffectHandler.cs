using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

public abstract class GameEffectHandler<TEffect> : IGameEffectHandler
    where TEffect : class, IGameEffect
{
    public Type EffectType => typeof(TEffect);

    public virtual int Order => 0;

    public Task ApplyAsync(
        IReadOnlyList<IGameEffect> effects,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var typed = new TEffect[effects.Count];
        for (var index = 0; index < effects.Count; index++)
            typed[index] = (TEffect)effects[index];
        return ApplyBatchAsync(typed, context, ct);
    }

    protected abstract Task ApplyBatchAsync(
        IReadOnlyList<TEffect> effects,
        IGameExecutionContext context,
        CancellationToken ct);
}
