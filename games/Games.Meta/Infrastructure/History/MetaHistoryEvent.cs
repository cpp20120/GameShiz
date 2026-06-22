namespace Games.Meta;

public sealed record MetaHistoryEvent(
    long Id,
    string EventType,
    string AggregateType,
    string AggregateId,
    long? SeasonId,
    long? ChatId,
    long? UserId,
    string PayloadJson,
    DateTimeOffset OccurredAt);
