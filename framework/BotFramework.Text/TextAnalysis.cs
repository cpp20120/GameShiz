namespace BotFramework.Text;

public sealed record TextAnalysis
{
    public required NormalizedText Text { get; init; }
    public required IReadOnlyList<AnalysisResult> Results { get; init; }
}
