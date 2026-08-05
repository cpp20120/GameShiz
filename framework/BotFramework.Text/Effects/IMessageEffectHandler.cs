namespace BotFramework.Text;

public interface IMessageEffectHandler
{
    Type EffectType { get; }

    ValueTask ExecuteAsync(
        IMessageEffect effect,
        TextProcessingContext context,
        CancellationToken cancellationToken = default);
}
