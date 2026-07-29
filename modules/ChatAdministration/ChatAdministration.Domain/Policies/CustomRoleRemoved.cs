using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record CustomRoleRemoved(ChatId ChatId, UserId UserId, RoleId RoleId) : DomainEvent
{
    public string EventType => nameof(CustomRoleRemoved);
}
