namespace BotFramework.Host.Analytics;

internal sealed record EventAnalyticsEnvelope(
    string StreamId,
    long StreamVersion,
    string AggregateType,
    int SchemaVersion,
    string CorrelationId,
    string CausationId);

internal static class EventAnalyticsEnvelopeAccessor
{
    private static readonly AsyncLocal<EventAnalyticsEnvelope?> CurrentEnvelope = new();

    public static EventAnalyticsEnvelope? Current
    {
        get => CurrentEnvelope.Value;
        set => CurrentEnvelope.Value = value;
    }
}
