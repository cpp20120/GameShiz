namespace BotFramework.Text;

/// <summary>
/// Type-safe base class used by transport adapters and consumer modules.
/// </summary>
public abstract class MessageEffectHandler<TEffect> : IMessageEffectHandler
    where TEffect : class, IMessageEffect
{
    public Type EffectType => typeof(TEffect);

    public ValueTask ExecuteAsync(
        IMessageEffect effect,
        TextProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        if (effect is not TEffect typedEffect)
        {
            throw new ArgumentException(
                $"Effect '{effect.GetType().FullName}' is not assignable to '{typeof(TEffect).FullName}'.",
                nameof(effect));
        }

        return ExecuteAsync(typedEffect, context, cancellationToken);
    }

    protected abstract ValueTask ExecuteAsync(
        TEffect effect,
        TextProcessingContext context,
        CancellationToken cancellationToken);
}
