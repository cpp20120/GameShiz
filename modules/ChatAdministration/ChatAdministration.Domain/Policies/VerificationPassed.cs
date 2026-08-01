using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record VerificationPassed(VerificationSession Session) : IDomainEvent
{
    public string EventType => nameof(VerificationPassed);
}
