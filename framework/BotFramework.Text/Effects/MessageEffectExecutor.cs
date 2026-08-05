namespace BotFramework.Text;

public sealed class MessageEffectExecutor : IMessageEffectExecutor
{
    private readonly IReadOnlyDictionary<Type, IMessageEffectHandler> _handlers;
    private readonly MessageEffectExecutorOptions _options;

    public MessageEffectExecutor(
        IEnumerable<IMessageEffectHandler>? handlers = null,
        MessageEffectExecutorOptions? options = null)
    {
        _options = options ?? new MessageEffectExecutorOptions();

        var registered = (handlers ?? []).ToArray();
        if (registered.Any(static handler => handler is null))
            throw new ArgumentException("The effect handler collection cannot contain null.", nameof(handlers));

        var duplicates = registered
            .GroupBy(handler => handler.EffectType)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key.FullName ?? group.Key.Name)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"Multiple message effect handlers are registered for: {string.Join(", ", duplicates)}.");
        }

        _handlers = registered.ToDictionary(handler => handler.EffectType);
    }

    public async ValueTask<MessageEffectExecutionReport> ExecuteAsync(
        IReadOnlyList<IMessageEffect> effects,
        TextProcessingContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(effects);
        ArgumentNullException.ThrowIfNull(context);

        if (effects.Count == 0)
            return MessageEffectExecutionReport.Empty;

        var executions = new List<MessageEffectExecution>(effects.Count);
        foreach (var effect in effects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (effect is null)
                throw new ArgumentNullException(nameof(effects), "The effects collection cannot contain null.");

            var handler = ResolveHandler(effect.GetType());
            if (handler is null)
            {
                if (_options.MissingHandlerBehavior == MissingMessageEffectHandlerBehavior.Throw)
                {
                    throw new InvalidOperationException(
                        $"No message effect handler is registered for '{effect.GetType().FullName}'.");
                }

                executions.Add(new MessageEffectExecution(
                    effect,
                    MessageEffectExecutionStatus.Skipped,
                    HandlerType: null));
                continue;
            }

            await handler.ExecuteAsync(effect, context, cancellationToken);
            executions.Add(new MessageEffectExecution(
                effect,
                MessageEffectExecutionStatus.Executed,
                handler.GetType().FullName));
        }

        return new MessageEffectExecutionReport { Items = executions.ToArray() };
    }

    private IMessageEffectHandler? ResolveHandler(Type effectType)
    {
        if (_handlers.TryGetValue(effectType, out var exact))
            return exact;

        var compatible = _handlers
            .Where(pair => pair.Key.IsAssignableFrom(effectType))
            .Select(pair => pair.Value)
            .ToArray();

        return compatible.Length switch
        {
            0 => null,
            1 => compatible[0],
            _ => throw new InvalidOperationException(
                $"More than one compatible message effect handler is registered for '{effectType.FullName}'."),
        };
    }
}
