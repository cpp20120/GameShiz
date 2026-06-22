namespace BotFramework.Host.Analytics;

public sealed record AnalyticsServiceStatus(
    bool Configured,
    bool Reachable,
    string? Error);
