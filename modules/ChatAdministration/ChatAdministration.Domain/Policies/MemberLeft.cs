using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record MemberLeft(ChatId ChatId, UserId UserId, string DisplayName, DateTimeOffset OccurredAt) : DomainEvent
{
    public string EventType => nameof(MemberLeft);
}
