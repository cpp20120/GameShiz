namespace BotFramework.Host.Analytics.Reports;

public sealed record AnalyticsReport(
    DateTime GeneratedAtUtc,
    string Project,
    string TableName,
    long TotalRowsAllTime,
    IReadOnlyList<AnalyticsWindowReport> Windows,
    IReadOnlyList<AnalyticsTimelineBucket> Timeline);
