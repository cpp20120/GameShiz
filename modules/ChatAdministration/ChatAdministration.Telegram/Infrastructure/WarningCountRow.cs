namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record WarningCountRow(
    long UserId,
    int ActiveWarningCount);
