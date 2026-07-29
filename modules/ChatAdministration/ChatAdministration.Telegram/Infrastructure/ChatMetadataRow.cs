namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record ChatMetadataRow(string ChatType, string Title, bool IsEnabled, string? SettingsJson, string? BotPermissionsJson, DateTime CreatedAt, DateTime UpdatedAt);
