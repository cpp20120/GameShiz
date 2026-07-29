using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record ScheduleEffect(
    DateTimeOffset ExecuteAt,
    EffectEnvelope Effect,
    string CorrelationId,
    string CausationId) : ModerationEffect;
