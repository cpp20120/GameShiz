namespace CasinoShiz.Host.Pages.Admin;

public sealed record ChatAdministrationSelectedChat(
    long ChatId,
    string ChatType,
    string Title,
    bool IsEnabled,
    string SettingsJson,
    string BotPermissionsJson,
    DateTime CreatedAt,
    DateTime UpdatedAt);
