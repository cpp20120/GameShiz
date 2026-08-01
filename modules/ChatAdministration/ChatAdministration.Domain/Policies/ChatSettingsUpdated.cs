using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record ChatSettingsUpdated(ChatId ChatId, ChatSettings Settings, DateTimeOffset UpdatedAt) : IDomainEvent
{
    public string EventType => nameof(ChatSettingsUpdated);
}
