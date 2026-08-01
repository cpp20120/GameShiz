using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record AutomatedModerationDecision(
    bool Accepted,
    string? ErrorCode,
    IReadOnlyList<Violation> Violations,
    ModerationCaseState? Case,
    IReadOnlyList<IDomainEvent> Events,
    EffectPlan EffectPlan,
    WarningState? Warning = null)
{
    public static AutomatedModerationDecision Ignore(string? errorCode = null) =>
        new(false, errorCode, [], null, [], new EffectPlan([]));
}
