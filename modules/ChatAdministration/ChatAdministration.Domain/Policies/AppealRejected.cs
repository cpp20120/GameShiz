using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record AppealRejected(AppealState Appeal) : IDomainEvent
{
    public string EventType => "appeal_rejected";
}
