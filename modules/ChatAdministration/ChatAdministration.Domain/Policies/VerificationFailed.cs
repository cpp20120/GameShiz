using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record VerificationFailed(VerificationSession Session, bool IsFinal) : DomainEvent
{
    public string EventType => nameof(VerificationFailed);
}
