namespace ChatAdministration.Domain.Effects;

public sealed record EffectPlan(IReadOnlyList<PlannedEffect> Effects);
