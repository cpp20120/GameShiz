using Telegram.Bot;

namespace BotFramework.Host.TelegramOutbox;

public sealed partial class TelegramOutboxDispatcherService(
    IServiceScopeFactory scopes,
    ITelegramBotClient bot,
    ILogger<TelegramOutboxDispatcherService> logger) : BackgroundService
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan EmptyDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                processed = await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogDispatcherFailed(ex);
            }

            var delay = processed == 0 ? EmptyDelay : PollDelay;
            try { await Task.Delay(delay, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<ITelegramOutboxStore>();
        var rows = await store.ClaimDueAsync(BatchSize, Lease, ct);

        foreach (var row in rows)
        {
            try
            {
                var sent = await bot.SendMessage(
                    row.ChatId,
                    row.Text,
                    parseMode: row.ParseMode,
                    cancellationToken: ct);
                await store.MarkSentAsync(row.Id, sent.MessageId, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                var retryAfter = ComputeRetryAfter(row.Attempts);
                await store.MarkFailedAsync(row.Id, ex.Message, retryAfter, ct);
                LogSendFailed(row.Id, retryAfter.TotalSeconds, ex);
            }
        }

        return rows.Count;
    }

    private static TimeSpan ComputeRetryAfter(int attempts)
    {
        var seconds = Math.Min(300, Math.Pow(2, Math.Clamp(attempts, 1, 8)));
        return TimeSpan.FromSeconds(seconds);
    }

    [LoggerMessage(LogLevel.Warning, "telegram_outbox.dispatcher_failed")]
    partial void LogDispatcherFailed(Exception ex);

    [LoggerMessage(LogLevel.Warning, "telegram_outbox.send_failed id={OutboxId} retry_after_seconds={RetryAfterSeconds}")]
    partial void LogSendFailed(long outboxId, double retryAfterSeconds, Exception ex);
}
