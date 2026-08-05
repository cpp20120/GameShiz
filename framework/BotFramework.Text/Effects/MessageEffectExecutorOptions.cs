namespace BotFramework.Text;

public sealed record MessageEffectExecutorOptions
{
    public MissingMessageEffectHandlerBehavior MissingHandlerBehavior { get; init; } =
        MissingMessageEffectHandlerBehavior.Throw;
}
