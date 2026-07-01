using BotFramework.Host.Contracts.Telegram;

namespace BotFramework.Host.TelegramOutbox;

public interface ITelegramOutboxStore : ITelegramOutbox
{
    Task<IReadOnlyList<TelegramOutboxAdminRow>> ListUnsentAsync(
        int limit,
        string? status,
        CancellationToken ct);

    Task<TelegramOutboxRescheduleResult> RescheduleNowAsync(long id, CancellationToken ct);

    Task<IReadOnlyList<TelegramOutboxRow>> ClaimDueAsync(int limit, TimeSpan lease, CancellationToken ct);

    Task MarkSentAsync(long id, int telegramMessageId, CancellationToken ct);

    Task MarkFailedAsync(long id, string errorMessage, TimeSpan retryAfter, CancellationToken ct);
}
