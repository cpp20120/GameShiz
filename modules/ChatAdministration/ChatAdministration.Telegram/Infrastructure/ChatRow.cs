namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record ChatRow(
    long ChatId,
    string ChatType,
    string Title,
    bool IsEnabled,
    string? SettingsJson,
    string? BotPermissionsJson,
    DateTime CreatedAt,
    DateTime UpdatedAt);
