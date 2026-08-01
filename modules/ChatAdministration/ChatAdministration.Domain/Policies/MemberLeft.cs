using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record MemberLeft(ChatId ChatId, UserId UserId, string DisplayName, DateTimeOffset OccurredAt) : IDomainEvent
{
    public string EventType => nameof(MemberLeft);
}
