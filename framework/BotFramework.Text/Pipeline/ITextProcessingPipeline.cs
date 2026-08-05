namespace BotFramework.Text;

/// <summary>
/// Platform-neutral entry point for text analysis and effectful message processing.
/// </summary>
public interface ITextProcessingPipeline
{
    ValueTask<TextPipelineResult> ProcessAsync(
        string text,
        TextProcessingContext? context = null,
        CancellationToken cancellationToken = default);

    ValueTask<TextPipelineResult> AnalyzeAsync(
        string text,
        TextProcessingContext? context = null,
        CancellationToken cancellationToken = default);
}
