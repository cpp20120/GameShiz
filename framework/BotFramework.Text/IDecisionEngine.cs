namespace BotFramework.Text;

public interface IDecisionEngine
{
    ValueTask<Decision> DecideAsync(
        TextAnalysis analysis,
        CancellationToken cancellationToken = default);
}
