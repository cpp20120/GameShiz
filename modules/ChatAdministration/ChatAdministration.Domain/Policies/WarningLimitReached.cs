using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record WarningLimitReached(
    ChatId ChatId,
    UserId UserId,
    int ActiveWarningCount,
    ModerationAction Action) : DomainEvent
{
    public string EventType => nameof(WarningLimitReached);
}
