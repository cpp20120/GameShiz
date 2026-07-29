using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record MemberJoined(MemberState Member, DateTimeOffset OccurredAt) : DomainEvent
{
    public string EventType => nameof(MemberJoined);
}
