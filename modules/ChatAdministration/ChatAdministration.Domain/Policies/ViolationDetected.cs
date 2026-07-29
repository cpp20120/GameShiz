using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record ViolationDetected(
    ChatId ChatId,
    UserId UserId,
    int MessageId,
    Violation Violation) : DomainEvent
{
    public string EventType => "violation_detected";
}
