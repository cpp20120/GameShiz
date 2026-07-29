using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record CustomRoleAssigned(ChatId ChatId, UserId UserId, RoleId RoleId) : DomainEvent
{
    public string EventType => nameof(CustomRoleAssigned);
}
