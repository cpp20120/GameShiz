using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record MemberRoleAssigned(
    ChatId ChatId,
    UserId UserId,
    ChatMemberRole Role) : DomainEvent
{
    public string EventType => "member_role_assigned";
}
