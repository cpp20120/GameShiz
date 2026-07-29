using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record CaseRevocationDecision(
    bool Accepted,
    string? ErrorCode,
    ModerationCaseState? Case,
    IReadOnlyList<DomainEvent> Events,
    EffectPlan EffectPlan)
{
    public static CaseRevocationDecision Reject(string errorCode) =>
        new(false, errorCode, null, [], new EffectPlan([]));
}
