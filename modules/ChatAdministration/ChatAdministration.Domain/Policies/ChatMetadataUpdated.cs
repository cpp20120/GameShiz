using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record ChatMetadataUpdated(
    ChatId ChatId,
    ChatType Type,
    string Title,
    DateTimeOffset OccurredAt) : IDomainEvent
{
    public string EventType => nameof(ChatMetadataUpdated);
}
