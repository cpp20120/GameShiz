using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record VerificationExpired(VerificationSession Session) : DomainEvent
{
    public string EventType => nameof(VerificationExpired);
}
