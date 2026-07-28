namespace BotFramework.Host.Analytics;

internal static class EventAnalyticsEnvelopeAccessor
{
    private static readonly AsyncLocal<EventAnalyticsEnvelope?> CurrentEnvelope = new();

    public static EventAnalyticsEnvelope? Current
    {
        get => CurrentEnvelope.Value;
        set => CurrentEnvelope.Value = value;
    }
}
