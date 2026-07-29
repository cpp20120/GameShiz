using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record ManualModerationDecision(
    bool Accepted,
    string? ErrorCode,
    ModerationCaseState? Case,
    WarningState? Warning,
    IReadOnlyList<DomainEvent> Events,
    EffectPlan EffectPlan)
{
    public static ManualModerationDecision Reject(string errorCode) =>
        new(false, errorCode, null, null, [], new EffectPlan([]));
}
