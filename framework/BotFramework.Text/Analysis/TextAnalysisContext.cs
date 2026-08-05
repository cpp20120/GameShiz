namespace BotFramework.Text;

/// <summary>
/// Input shared by analyzers. It combines normalized text with transport-neutral message metadata.
/// </summary>
public sealed record TextAnalysisContext
{
    public required NormalizedText Text { get; init; }
    public required TextProcessingContext ProcessingContext { get; init; }
}
