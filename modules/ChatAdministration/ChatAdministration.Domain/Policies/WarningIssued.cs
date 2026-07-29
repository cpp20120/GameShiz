using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record WarningIssued(WarningState Warning) : DomainEvent
{
    public string EventType => "warning_issued";
}
