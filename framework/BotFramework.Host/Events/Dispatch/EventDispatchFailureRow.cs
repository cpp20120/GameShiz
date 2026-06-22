namespace BotFramework.Host.Events;

public sealed record EventDispatchFailureRow(
    long Id,
    string StreamId,
    long StreamVersion,
    string EventType,
    string Stage,
    string HandlerName,
    string Error,
    string? ErrorType,
    int RetryCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt);
