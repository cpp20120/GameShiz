using System.Text.Json;

namespace BotFramework.Host.Execution;

internal sealed partial class GameEventOutboxDispatcher(
    PostgresGameEventOutbox outbox,
    IDomainEventBus events,
    ILogger<GameEventOutboxDispatcher> logger,
    TimeProvider timeProvider) : BackgroundService
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

    private async Task DispatchBatchAsync(CancellationToken ct)
    {
        var batch = await outbox.ClaimAsync(50, TimeSpan.FromMinutes(1), ct).ConfigureAwait(false);
        foreach (var item in batch)
        {
            try
            {
                var type = Type.GetType(item.TypeName, throwOnError: true)!;
                var domainEvent = JsonSerializer.Deserialize(item.Payload, type, JsonOptions) as IDomainEvent
                    ?? throw new InvalidOperationException($"Outbox payload is not an {nameof(IDomainEvent)}.");
                await events.PublishAsync(domainEvent, ct).ConfigureAwait(false);
                await outbox.MarkSentAsync(item.Id, ct).ConfigureAwait(false);
                GameExecutionTelemetry.RecordOutboxLag(item.CreatedAt, timeProvider.GetUtcNow());
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                LogDeliveryFailed(logger, exception, item.Id, item.Attempts);
                await outbox.MarkFailedAsync(item.Id, exception.Message, item.Attempts, ct).ConfigureAwait(false);
            }
        }
    }

    [LoggerMessage(LogLevel.Warning, "game.event.outbox.delivery_failed id={OutboxId} attempts={Attempts}")]
    private static partial void LogDeliveryFailed(ILogger logger, Exception exception, long outboxId, int attempts);

    [LoggerMessage(LogLevel.Warning, "game.event.outbox.poll_failed")]
    private static partial void LogPollFailed(ILogger logger, Exception exception);
}
