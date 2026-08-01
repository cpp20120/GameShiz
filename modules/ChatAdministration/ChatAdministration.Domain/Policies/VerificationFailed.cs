using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record VerificationFailed(VerificationSession Session, bool IsFinal) : IDomainEvent
{
    public string EventType => nameof(VerificationFailed);
}
