namespace BotFramework.Host.Events;

public sealed record DomainEventEnvelope(
    string EventType,
    string TypeName,
    string Payload,
    long OccurredAt);
