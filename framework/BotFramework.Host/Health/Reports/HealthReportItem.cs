using BotFramework.Sdk;

namespace BotFramework.Host.Health;

public sealed record HealthReportItem(string Name, HealthCheckKind Kind, bool Healthy, string? Detail);
