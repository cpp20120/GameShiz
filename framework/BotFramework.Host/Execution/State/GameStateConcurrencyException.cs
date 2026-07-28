namespace BotFramework.Host.Execution;

public sealed class GameStateConcurrencyException : InvalidOperationException
{
    public GameStateConcurrencyException()
        : this(string.Empty, string.Empty, 0)
    {
    }

    public GameStateConcurrencyException(string message)
        : base(message)
    {
    }

    public GameStateConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public GameStateConcurrencyException(string gameId, string aggregateId, long expectedRevision)
        : base($"Game aggregate '{gameId}:{aggregateId}' is not at expected revision {expectedRevision}.")
    {
        GameId = gameId;
        AggregateId = aggregateId;
        ExpectedRevision = expectedRevision;
    }

    public string GameId { get; } = string.Empty;

    public string AggregateId { get; } = string.Empty;

    public long ExpectedRevision { get; }
}
