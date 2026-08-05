namespace BotFramework.Text;

public interface ITextAnalyzer
{
    string Name { get; }
    int Order { get; }

    ValueTask<AnalysisResult> AnalyzeAsync(
        NormalizedText text,
        CancellationToken cancellationToken = default);
}
