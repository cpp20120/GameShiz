namespace BotFramework.Host.Events.Serialization;

public sealed record DomainEventEnvelope(
    string EventType,
    string TypeName,
    string Payload,
    long OccurredAt,
    string StreamId = "",
    long StreamVersion = 0,
    string AggregateType = "",
    int SchemaVersion = 1,
    string CorrelationId = "",
    string CausationId = "");
