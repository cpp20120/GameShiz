namespace BotFramework.Host.Analytics;

public interface IAnalyticsQueryService
{
    Task<AnalyticsServiceStatus> GetStatusAsync(CancellationToken ct);
    Task<AnalyticsReport> GetReportAsync(int topN, int timelineDays, CancellationToken ct);
}
