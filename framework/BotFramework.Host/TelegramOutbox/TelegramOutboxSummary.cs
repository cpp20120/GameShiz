namespace BotFramework.Host.TelegramOutbox;

public sealed record TelegramOutboxSummary(
    int PendingCount,
    int SendingCount,
    int DueCount,
    int ExpiredLeaseCount,
    DateTimeOffset? OldestUnsentAt);
