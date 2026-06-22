namespace BotFramework.Host.Analytics;

public sealed record AnalyticsReport(
    DateTime GeneratedAtUtc,
    string Project,
    string TableName,
    long TotalRowsAllTime,
    IReadOnlyList<AnalyticsWindowReport> Windows,
    IReadOnlyList<AnalyticsTimelineBucket> Timeline);
