using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record MemberRoleRemoved(
    ChatId ChatId,
    UserId UserId,
    ChatMemberRole Role) : IDomainEvent
{
    public string EventType => "member_role_removed";
}
