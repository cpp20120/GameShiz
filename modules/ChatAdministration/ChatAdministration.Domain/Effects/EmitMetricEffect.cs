namespace ChatAdministration.Domain.Effects;

public sealed record EmitMetricEffect(
    string Name,
    IReadOnlyDictionary<string, string> Labels,
    string CorrelationId,
    string CausationId) : IModerationEffect;
