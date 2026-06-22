namespace BotFramework.Host.Events;

public sealed record EventDispatchFailure(
    string StreamId,
    long StreamVersion,
    string EventType,
    string Stage,
    string HandlerName,
    string Error,
    string? ErrorType);
