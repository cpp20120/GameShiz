namespace BotFramework.Text;

public interface IMessageEffectExecutor
{
    ValueTask<MessageEffectExecutionReport> ExecuteAsync(
        IReadOnlyList<IMessageEffect> effects,
        TextProcessingContext context,
        CancellationToken cancellationToken = default);
}
