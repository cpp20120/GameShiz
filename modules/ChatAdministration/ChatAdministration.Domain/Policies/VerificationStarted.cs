using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record VerificationStarted(VerificationSession Session) : DomainEvent
{
    public string EventType => nameof(VerificationStarted);
}
