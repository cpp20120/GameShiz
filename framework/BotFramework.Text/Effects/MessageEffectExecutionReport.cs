namespace BotFramework.Text;

public sealed record MessageEffectExecutionReport
{
    public IReadOnlyList<MessageEffectExecution> Items { get; init; } = [];
    public static MessageEffectExecutionReport Empty { get; } = new();
}
