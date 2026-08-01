using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record BotPermissionsObserved(
    ChatId ChatId,
    TelegramBotPermissions Permissions,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public string EventType => nameof(BotPermissionsObserved);
}
