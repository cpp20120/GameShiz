namespace BotFramework.Text;

public interface IAnalysisObserver
{
    ValueTask ObserveAsync(
        TextPipelineResult result,
        CancellationToken cancellationToken = default);
}
