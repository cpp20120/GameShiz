namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record SettingsCallbackRow(
    string Token,
    long ChatId,
    string Key,
    string Value,
    DateTime ExpiresAt);
