using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record WarningLimitReached(
    ChatId ChatId,
    UserId UserId,
    int ActiveWarningCount,
    ModerationAction Action) : IDomainEvent
{
    public string EventType => nameof(WarningLimitReached);
}
