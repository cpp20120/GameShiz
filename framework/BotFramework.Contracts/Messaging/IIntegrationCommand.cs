namespace BotFramework.Contracts.Messaging;

/// <summary>
/// A durable request for a state transition owned by another service.
/// Commands are delivered at least once and must be idempotent by operation ID.
/// </summary>
public interface IIntegrationCommand
{
    string CommandType { get; }
    DateTimeOffset OccurredAt { get; }
}
