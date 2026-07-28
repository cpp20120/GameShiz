namespace BotFramework.Host.Analytics;

internal sealed record EventAnalyticsEnvelope(
    string StreamId,
    long StreamVersion,
    string AggregateType,
    int SchemaVersion,
    string CorrelationId,
    string CausationId);
