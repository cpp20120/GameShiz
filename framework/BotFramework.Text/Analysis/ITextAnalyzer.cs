namespace BotFramework.Text;

public interface ITextAnalyzer
{
    string Name { get; }
    int Order { get; }

    ValueTask<AnalysisResult> AnalyzeAsync(
        TextAnalysisContext context,
        CancellationToken cancellationToken = default);
}
