namespace BotFramework.Host.Execution;

public sealed class GameStateConcurrencyException(
    string gameId,
    string aggregateId,
    long expectedRevision)
    : InvalidOperationException(
        $"Game aggregate '{gameId}:{aggregateId}' is not at expected revision {expectedRevision}.")
{
    public string GameId { get; } = gameId;

    public string AggregateId { get; } = aggregateId;

    public long ExpectedRevision { get; } = expectedRevision;
}
