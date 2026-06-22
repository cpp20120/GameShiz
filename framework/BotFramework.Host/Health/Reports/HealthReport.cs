namespace BotFramework.Host.Health;

public sealed record HealthReport(bool Healthy, IReadOnlyList<HealthReportItem> Checks);
