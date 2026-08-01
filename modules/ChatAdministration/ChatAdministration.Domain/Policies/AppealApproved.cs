using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record AppealApproved(AppealState Appeal) : IDomainEvent
{
    public string EventType => "appeal_approved";
}
