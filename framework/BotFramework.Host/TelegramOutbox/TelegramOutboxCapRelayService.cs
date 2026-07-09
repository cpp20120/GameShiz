using DotNetCore.CAP;
using System.Threading.Channels;

namespace BotFramework.Host.TelegramOutbox;

/// <summary>Claims database outbox rows and hands them to the CAP transport.</summary>
public sealed partial class TelegramOutboxCapRelayService(
    ITelegramOutboxStore store,
    ICapPublisher publisher,
    ILogger<TelegramOutboxCapRelayService> logger) : BackgroundService
{
    public const string DeliveryTopic = "telegram.outbox.delivery-requested";

    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan EmptyDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);
    private const int BatchSize = 20;
    private readonly Channel<byte> _wakeups = Channel.CreateBounded<byte>(1);

    /// <summary>Requests an immediate relay poll; repeated signals are coalesced.</summary>
    public void Signal() => _wakeups.Writer.TryWrite(0);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var relayed = 0;
            try
            {
                var rows = await store.ClaimDueAsync(BatchSize, Lease, stoppingToken);
                foreach (var row in rows)
                {
                    await publisher.PublishAsync(
                        DeliveryTopic,
                        new TelegramOutboxDeliveryRequested(row.Id, row.ChatId, row.Text, row.ParseMode),
                        cancellationToken: stoppingToken);
                }
                relayed = rows.Count;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                LogRelayFailed(ex);
            }

            try
            {
                var wakeup = _wakeups.Reader.WaitToReadAsync(stoppingToken).AsTask();
                await Task.WhenAny(Task.Delay(relayed == 0 ? EmptyDelay : PollDelay, stoppingToken), wakeup);
                while (_wakeups.Reader.TryRead(out _)) { }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    [LoggerMessage(LogLevel.Warning, "telegram_outbox.cap_relay_failed")]
    private partial void LogRelayFailed(Exception exception);
}
