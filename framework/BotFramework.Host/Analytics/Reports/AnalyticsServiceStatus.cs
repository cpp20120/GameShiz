namespace BotFramework.Host.Analytics.Reports;

public sealed record AnalyticsServiceStatus(
    bool Configured,
    bool Reachable,
    string? Error);
