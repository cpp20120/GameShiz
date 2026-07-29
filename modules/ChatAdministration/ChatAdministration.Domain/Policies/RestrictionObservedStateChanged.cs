using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record RestrictionObservedStateChanged(
    ChatId ChatId,
    UserId UserId,
    RestrictionState? State) : DomainEvent
{
    public string EventType => "restriction.observed_state_changed";
}
