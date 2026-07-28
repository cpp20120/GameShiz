namespace BotFramework.Host.TelegramOutbox;

/// <summary>Read-only operational port; delivery mutation stays on the outbox store.</summary>
public interface ITelegramOutboxMonitor
{
    Task<TelegramOutboxSummary> GetSummaryAsync(CancellationToken ct);
}
