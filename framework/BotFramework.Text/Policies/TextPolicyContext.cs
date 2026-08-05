namespace BotFramework.Text;

public sealed record TextPolicyContext
{
    public required TextAnalysis Analysis { get; init; }
}
