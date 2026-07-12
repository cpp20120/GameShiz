using System.Text.Json;
using BotFramework.Scheduling.Abstractions;

namespace BotFramework.Host.Execution;

internal sealed partial class GameScheduleOutboxDispatcher(
    PostgresGameScheduleOutbox outbox,
    IGameScheduler scheduler,
    ILogger<GameScheduleOutboxDispatcher> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
        do
        {
            try
            {
                await DispatchBatchAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogPollFailed(logger, exception);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    internal async Task DispatchBatchAsync(CancellationToken ct)
    {
        var batch = await outbox.ClaimAsync(50, TimeSpan.FromMinutes(1), ct).ConfigureAwait(false);
        foreach (var item in batch)
        {
            try
            {
                await ApplyAsync(item, ct).ConfigureAwait(false);
                await outbox.MarkSentAsync(item.Id, ct).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogDeliveryFailed(logger, exception, item.Id, item.Attempts);
                await outbox.MarkFailedAsync(item.Id, exception.Message, item.Attempts, ct).ConfigureAwait(false);
            }
        }
    }

    private async Task ApplyAsync(GameScheduleOutboxItem item, CancellationToken ct)
    {
        if (string.Equals(item.EffectKind, "cancel", StringComparison.Ordinal))
        {
            await scheduler.UnscheduleAsync(item.ScheduleId, ct).ConfigureAwait(false);
            return;
        }

        if (!string.Equals(item.EffectKind, "schedule", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(item.JobKey)
            || item.DueAtUnixMilliseconds is null)
        {
            throw new InvalidOperationException($"Invalid schedule outbox item '{item.Id}'.");
        }

        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(item.Data, JsonOptions)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
        await scheduler.ScheduleAsync(
            new GameScheduleCommand(
                item.ScheduleId,
                item.JobKey,
                ScheduleDescriptor.Once(
                    DateTimeOffset.FromUnixTimeMilliseconds(item.DueAtUnixMilliseconds.Value)),
                data),
            ct).ConfigureAwait(false);
    }

    [LoggerMessage(LogLevel.Warning, "game.schedule.outbox.delivery_failed id={OutboxId} attempts={Attempts}")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception exception, long outboxId, int attempts);

    [LoggerMessage(LogLevel.Warning, "game.schedule.outbox.poll_failed")]
    private static partial void LogPollFailed(ILogger logger, Exception exception);
}
