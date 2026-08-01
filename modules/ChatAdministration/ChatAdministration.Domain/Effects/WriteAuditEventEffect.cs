namespace ChatAdministration.Domain.Effects;

public sealed record WriteAuditEventEffect(
    AuditEventPayload Event,
    string CorrelationId,
    string CausationId) : IModerationEffect;
