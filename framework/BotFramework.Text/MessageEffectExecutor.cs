namespace BotFramework.Text;

public sealed class MessageEffectExecutor(
    IEnumerable<IMessageEffectHandler> handlers) : IMessageEffectExecutor
{
    private readonly IMessageEffectHandler[] _handlers = handlers
        .OrderBy(handler => handler.EffectType.FullName, StringComparer.Ordinal)
        .ToArray();

    public async ValueTask ExecuteAsync(
        IReadOnlyList<IMessageEffect> effects,
        TextProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(context);

        foreach (var effect in effects)
        {
            if (effect is null)
                throw new ArgumentNullException(nameof(effects), "The effects collection cannot contain null.");

            var handler = _handlers.FirstOrDefault(candidate => candidate.EffectType.IsInstanceOfType(effect));
            if (handler is null)
            {
                throw new InvalidOperationException(
                    $"No message effect handler is registered for '{effect.GetType().FullName}'.");
            }

            await handler.ExecuteAsync(effect, context, cancellationToken);
        }
    }
}
