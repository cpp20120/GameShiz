namespace ChatAdministration.Domain.Effects;

public sealed record AppendDomainEventsEffect(
    string AggregateId,
    IReadOnlyCollection<DomainEventPayload> Events,
    string CorrelationId,
    string CausationId) : ModerationEffect;
