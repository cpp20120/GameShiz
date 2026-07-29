namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record AnalyticsRow(
    long Cases,
    long AppliedCases,
    long FailedCases,
    long UnknownCases,
    long ActiveWarnings,
    long IndexedMessages);
