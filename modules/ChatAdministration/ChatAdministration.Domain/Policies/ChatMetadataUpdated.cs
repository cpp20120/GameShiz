using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record ChatMetadataUpdated(
    ChatId ChatId,
    ChatType Type,
    string Title,
    DateTimeOffset OccurredAt) : DomainEvent
{
    public string EventType => nameof(ChatMetadataUpdated);
}
