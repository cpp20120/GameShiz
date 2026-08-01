using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record VerificationDecision(
    bool Accepted,
    string? ErrorCode,
    VerificationSession? Session,
    IReadOnlyCollection<IDomainEvent> Events,
    EffectPlan EffectPlan)
{
    public static VerificationDecision Reject(string errorCode) =>
        new(false, errorCode, null, [], new EffectPlan([]));
}
