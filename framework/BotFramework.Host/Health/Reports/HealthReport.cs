namespace BotFramework.Host.Health.Reports;

public sealed record HealthReport(bool Healthy, IReadOnlyList<HealthReportItem> Checks);
