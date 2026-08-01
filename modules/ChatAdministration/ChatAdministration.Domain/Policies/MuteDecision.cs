using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record MuteDecision(
    bool Accepted,
    string? ErrorCode,
    ModerationCaseState? Case,
    RestrictionState? DesiredRestriction,
    IReadOnlyList<IDomainEvent> Events,
    EffectPlan EffectPlan)
{
    public static MuteDecision Reject(string errorCode) => new(false, errorCode, null, null, [], new EffectPlan([]));
}
