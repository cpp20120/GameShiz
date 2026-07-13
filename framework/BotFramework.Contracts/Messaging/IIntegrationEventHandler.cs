namespace BotFramework.Contracts.Messaging;

/// <summary>
/// Transport-neutral subscriber for a published integration fact.
/// Implementations must tolerate duplicate delivery.
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task HandleAsync(TEvent integrationEvent, CancellationToken ct);
}
