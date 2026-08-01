using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record MemberJoined(MemberState Member, DateTimeOffset OccurredAt) : IDomainEvent
{
    public string EventType => nameof(MemberJoined);
}
