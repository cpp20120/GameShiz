namespace BotFramework.Host.TelegramOutbox;

public sealed record TelegramOutboxAdminRow(
    long Id,
    long ChatId,
    string Status,
    int Attempts,
    DateTimeOffset NextAttemptAt,
    DateTimeOffset? LockedUntil,
    string? LastError,
    string MessagePreview,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
