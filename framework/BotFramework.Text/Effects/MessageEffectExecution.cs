namespace BotFramework.Text;

public sealed record MessageEffectExecution(
    IMessageEffect Effect,
    MessageEffectExecutionStatus Status,
    string? HandlerType);
