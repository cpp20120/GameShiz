using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record VerificationStarted(VerificationSession Session) : IDomainEvent
{
    public string EventType => nameof(VerificationStarted);
}
