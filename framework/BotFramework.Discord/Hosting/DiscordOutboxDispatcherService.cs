using BotFramework.Host.DiscordOutbox;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace BotFramework.Discord.Hosting;

public sealed partial class DiscordOutboxDispatcherService(
    DiscordSocketClient client,
    IServiceScopeFactory scopes,
    ILogger<DiscordOutboxDispatcherService> logger) : BackgroundService
{
    private static readonly TimeSpan PollDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                if (client.ConnectionState == ConnectionState.Connected)
                    processed = await DispatchBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                LogDispatcherFailed(logger, exception);
            }

            try
            {
                await Task.Delay(processed == 0 ? TimeSpan.FromSeconds(5) : PollDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IDiscordOutboxStore>();
        var rows = await store.ClaimDueAsync(BatchSize, Lease, ct);

        foreach (var row in rows)
        {
            try
            {
                var channel = await ResolveChannelAsync(row, ct);
                if (channel is null)
                    throw new InvalidOperationException($"Discord channel {row.ChannelId} is unavailable.");

                var sent = await channel.SendMessageAsync(
                    text: null,
                    embed: DiscordEmbeds.Text(row.Text, row.Title, row.Culture),
                    options: new RequestOptions { CancelToken = ct });
                await store.MarkSentAsync(row.Id, checked((long)sent.Id), ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                var retryAfter = ComputeRetryAfter(row.Attempts);
                await store.MarkFailedAsync(row.Id, exception.Message, retryAfter, ct);
                LogSendFailed(logger, row.Id, retryAfter.TotalSeconds, exception);
            }
        }

        return rows.Count;
    }

    private async Task<IMessageChannel?> ResolveChannelAsync(
        DiscordOutboxRow row,
        CancellationToken ct)
    {
        if (client.GetChannel((ulong)row.ChannelId) is IMessageChannel channel)
            return channel;

        if (row.UserId == 0 || client.GetUser((ulong)row.UserId) is not { } user)
            return null;

        return await user.CreateDMChannelAsync(new RequestOptions { CancelToken = ct });
    }

    private static TimeSpan ComputeRetryAfter(int attempts) =>
        TimeSpan.FromSeconds(Math.Min(300, Math.Pow(2, Math.Clamp(attempts, 1, 8))));

    [LoggerMessage(LogLevel.Warning, "discord_outbox.dispatcher_failed")]
    private static partial void LogDispatcherFailed(ILogger logger, Exception exception);

    [LoggerMessage(LogLevel.Warning, "discord_outbox.send_failed id={OutboxId} retry_after_seconds={RetryAfterSeconds}")]
    private static partial void LogSendFailed(ILogger logger, long outboxId, double retryAfterSeconds, Exception exception);
}
