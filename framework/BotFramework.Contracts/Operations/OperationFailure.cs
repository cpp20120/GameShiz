namespace BotFramework.Contracts.Operations;

public sealed record OperationFailure(long Id, string StreamId, long StreamVersion, string EventType,
    string Stage, string HandlerName, string Error, string? ErrorType, int RetryCount,
    DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt);
