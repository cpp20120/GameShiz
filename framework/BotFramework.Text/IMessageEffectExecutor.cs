namespace BotFramework.Text;

public interface IMessageEffectExecutor
{
    ValueTask ExecuteAsync(
        IReadOnlyList<IMessageEffect> effects,
        TextProcessingContext context,
        CancellationToken cancellationToken = default);
}
