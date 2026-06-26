
namespace BotFramework.Host.Health.Reports;

public sealed record HealthReportItem(string Name, HealthCheckKind Kind, bool Healthy, string? Detail);
