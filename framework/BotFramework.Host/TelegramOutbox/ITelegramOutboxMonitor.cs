namespace BotFramework.Host.TelegramOutbox;

public sealed record TelegramOutboxSummary(
    int PendingCount,
    int SendingCount,
    int DueCount,
    int ExpiredLeaseCount,
    DateTimeOffset? OldestUnsentAt);

/// <summary>Read-only operational port; delivery mutation stays on the outbox store.</summary>
public interface ITelegramOutboxMonitor
{
    Task<TelegramOutboxSummary> GetSummaryAsync(CancellationToken ct);
}
