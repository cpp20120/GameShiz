using ChatAdministration.Domain.Effects;

namespace ChatAdministration.Domain.Policies;

public sealed record MemberLifecycleDecision(
    bool Accepted,
    string? ErrorCode,
    IReadOnlyCollection<IDomainEvent> Events,
    EffectPlan EffectPlan)
{
    public static MemberLifecycleDecision Reject(string errorCode) =>
        new(false, errorCode, [], new EffectPlan([]));
}
