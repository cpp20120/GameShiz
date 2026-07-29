using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record BotPermissionsObserved(
    ChatId ChatId,
    TelegramBotPermissions Permissions,
    DateTimeOffset OccurredAt) : DomainEvent
{
    public string EventType => nameof(BotPermissionsObserved);
}
