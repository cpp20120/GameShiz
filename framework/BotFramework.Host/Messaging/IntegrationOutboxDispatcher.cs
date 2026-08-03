using System.Text.Json;
using BotFramework.Contracts.Messaging;
using DotNetCore.CAP;

namespace BotFramework.Host.Messaging;

public sealed partial class IntegrationOutboxDispatcher(
    IServiceScopeFactory scopeFactory,
    IntegrationInboxOptions options,
    ILogger<IntegrationOutboxDispatcher> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var leaseOwner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IIntegrationOutboxStore>();
                var batch = await store.ClaimAsync(options.ConsumerName, 50, Lease, leaseOwner, stoppingToken);
                BotFramework.Contracts.Observability.BotFrameworkMetrics.SetIntegrationOutboxDepth(
                    await store.CountPendingAsync(options.ConsumerName, stoppingToken));
                if (batch.Count == 0)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                    continue;
                }

                var publisher = scope.ServiceProvider.GetRequiredService<ICapPublisher>();
                foreach (var message in batch)
                    await PublishOneAsync(store, publisher, message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogPollFailed(exception);
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task PublishOneAsync(
        IIntegrationOutboxStore store,
        ICapPublisher publisher,
        IntegrationOutboxDelivery message,
        CancellationToken ct)
    {
        try
        {
            object envelope = message.Kind switch
            {
                IntegrationMessageKind.Event => JsonSerializer.Deserialize<IntegrationEventEnvelope>(
                    message.EnvelopeJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new InvalidOperationException("Integration event envelope is empty."),
                IntegrationMessageKind.Command => JsonSerializer.Deserialize<IntegrationCommandEnvelope>(
                    message.EnvelopeJson, new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new InvalidOperationException("Integration command envelope is empty."),
                _ => throw new InvalidOperationException($"Unsupported integration message kind '{message.Kind}'."),
            };

            var headers = new Dictionary<string, string?>
            {
                ["cap-kafka-key"] = message.MessageKey,
            };
            await publisher.PublishAsync(message.Topic, envelope, headers, ct);
            await store.MarkPublishedAsync(message.ProducerName, message.MessageId, message.LeaseOwner, ct);
            BotFramework.Contracts.Observability.BotFrameworkMetrics.IntegrationOutboxPublished.Add(1);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await store.MarkFailedAsync(message.ProducerName, message.MessageId, message.LeaseOwner, exception.Message, ct);
            BotFramework.Contracts.Observability.BotFrameworkMetrics.IntegrationOutboxFailures.Add(1);
            LogPublishFailed(exception, message.MessageId, message.Topic);
        }
    }

    [LoggerMessage(LogLevel.Warning, "integration_outbox.poll_failed")]
    private partial void LogPollFailed(Exception exception);

    [LoggerMessage(LogLevel.Warning, "integration_outbox.publish_failed message_id={MessageId} topic={Topic}")]
    private partial void LogPublishFailed(Exception exception, string messageId, string topic);
}
