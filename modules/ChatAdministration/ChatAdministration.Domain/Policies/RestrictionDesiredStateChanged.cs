using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record RestrictionDesiredStateChanged(
    ChatId ChatId,
    UserId UserId,
    RestrictionState State,
    ModerationCaseId CaseId) : IDomainEvent
{
    public string EventType => "restriction_desired_state_changed";
}
