namespace ChatAdministration.Domain.Effects;

public sealed record WriteStructuredLogEffect(
    string EventName,
    string Level,
    IReadOnlyDictionary<string, object?> Properties,
    string CorrelationId,
    string CausationId) : ModerationEffect;
