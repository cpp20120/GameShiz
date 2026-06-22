namespace BotFramework.Host.Analytics;

public sealed record AnalyticsWindowReport(
    string Label,
    TimeSpan Window,
    long TotalEvents,
    long DistinctUsers,
    IReadOnlyList<AnalyticsCount> TopEventTypes,
    IReadOnlyList<AnalyticsCount> TopModules,
    IReadOnlyList<AnalyticsUserCount> TopUsers);
