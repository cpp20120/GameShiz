namespace BotFramework.Host.Analytics;

public static class AnalyticsContextAccessor
{
    private static readonly AsyncLocal<IReadOnlyDictionary<string, object?>?> CurrentContext = new();

    public static IReadOnlyDictionary<string, object?>? Current
    {
        get => CurrentContext.Value;
        set => CurrentContext.Value = value;
    }
}
