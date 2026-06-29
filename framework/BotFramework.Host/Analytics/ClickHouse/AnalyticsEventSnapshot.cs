namespace BotFramework.Host.Analytics.ClickHouse;

internal sealed record AnalyticsEventSnapshot(
    Guid EventId,
    string EventType,
    string Module,
    long UserId,
    IReadOnlyDictionary<string, string> Params,
    string StreamId,
    long StreamVersion,
    string AggregateType,
    int SchemaVersion,
    string CorrelationId,
    string CausationId,
    bool IsReplay,
    DateTime OccurredAt);
