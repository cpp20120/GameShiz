using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record WarningIssued(WarningState Warning) : IDomainEvent
{
    public string EventType => "warning_issued";
}
