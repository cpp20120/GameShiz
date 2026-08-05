namespace BotFramework.Text;

public sealed record TextPipelineResult
{
    public required TextProcessingContext Context { get; init; }
    public required NormalizedText Text { get; init; }
    public required TextAnalysis Analysis { get; init; }
    public required Decision Decision { get; init; }
}
