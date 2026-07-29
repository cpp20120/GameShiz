namespace ChatAdministration.Domain.Effects;

public sealed record PersistAggregateEffect(
    string AggregateType,
    string AggregateId,
    string StateJson,
    string CorrelationId,
    string CausationId) : ModerationEffect;
