using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record AppealApproved(AppealState Appeal) : DomainEvent
{
    public string EventType => "appeal_approved";
}
