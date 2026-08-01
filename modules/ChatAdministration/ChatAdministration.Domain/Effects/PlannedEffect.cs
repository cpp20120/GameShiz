using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record PlannedEffect(
    IModerationEffect Effect,
    EffectImportance Importance,
    IReadOnlyCollection<EffectId> DependsOn,
    EffectId? CompensationEffectId = null,
    EffectId? Id = null);
