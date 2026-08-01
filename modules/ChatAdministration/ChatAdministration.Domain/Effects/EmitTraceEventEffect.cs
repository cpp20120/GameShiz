namespace ChatAdministration.Domain.Effects;

public sealed record EmitTraceEventEffect(
    string Name,
    IReadOnlyDictionary<string, string> Attributes,
    string CorrelationId,
    string CausationId) : IModerationEffect;
