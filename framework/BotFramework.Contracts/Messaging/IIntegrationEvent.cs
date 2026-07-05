namespace BotFramework.Contracts.Messaging;

/// <summary>
/// A fact published after state has changed. Integration events are asynchronous,
/// may have multiple consumers, and must be handled idempotently.
/// </summary>
public interface IIntegrationEvent
{
    string EventType { get; }
    DateTimeOffset OccurredAt { get; }
}
