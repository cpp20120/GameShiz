namespace BotFramework.Contracts.Operations;

public sealed record OperationOutbox(long Id, long ChatId, string Status, int Attempts,
    DateTimeOffset NextAttemptAt, DateTimeOffset? LockedUntil, string? LastError, string MessagePreview,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
