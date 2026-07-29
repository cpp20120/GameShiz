using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record AppealOpened(AppealState Appeal) : DomainEvent
{
    public string EventType => "appeal_opened";
}
