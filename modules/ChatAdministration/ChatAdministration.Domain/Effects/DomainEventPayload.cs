namespace ChatAdministration.Domain.Effects;

public sealed record DomainEventPayload(string EventType, string PayloadJson);
