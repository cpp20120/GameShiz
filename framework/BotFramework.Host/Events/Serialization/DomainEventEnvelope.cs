namespace BotFramework.Host.Events.Serialization;

public sealed record DomainEventEnvelope(
    string EventType,
    string TypeName,
    string Payload,
    long OccurredAt);
