namespace BotFramework.Text;

public sealed record TextAnalysis
{
    public required TextProcessingContext ProcessingContext { get; init; }
    public required NormalizedText Text { get; init; }
    public required IReadOnlyList<AnalysisResult> Results { get; init; }
}
