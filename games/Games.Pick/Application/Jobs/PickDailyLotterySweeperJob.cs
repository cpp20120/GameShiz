// ─────────────────────────────────────────────────────────────────────────────
// PickDailyLotterySweeperJob — IBackgroundJob that drains daily lotteries
// whose deadline (next-local-midnight UTC instant) has passed and posts the
// outcome in the originating chat.
//
// Same pattern as PickLotterySweeperJob but on the daily table. Runs on a
// minute-ish cadence (configurable). One missed minute is harmless: rows
// stay in 'open' and the next tick picks them up.
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.Extensions.Options;

namespace Games.Pick.Application.Jobs;

public sealed partial class PickDailyLotterySweeperJob(
    IPickDailyLotteryStore store,
    IPickDailyLotteryService service,
    IPickAnnouncementPublisher announcements,
    IOptions<PickOptions> options,
    ILogger<PickDailyLotterySweeperJob> logger) : IBackgroundJob
{
    public string Name => "pick.daily.lottery.sweeper";

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(15, options.Value.Daily.SweeperIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var expired = await store.ListExpiredOpenAsync(limit: 32, stoppingToken);
                foreach (var row in expired)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    try
                    {
                        var settle = await service.SettleAsync(row, stoppingToken);
                        await announcements.PublishDailyAsync(settle, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception ex) { LogSettleFailed(row.Id, row.ChatId, ex); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { LogTickFailed(ex); }

            try { await Task.Delay(interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }

    [LoggerMessage(EventId = 5971, Level = LogLevel.Error,
        Message = "pick.daily.sweeper.tick_failed")]
    partial void LogTickFailed(Exception ex);

    [LoggerMessage(EventId = 5972, Level = LogLevel.Warning,
        Message = "pick.daily.sweeper.settle_failed lottery={LotteryId} chat={ChatId}")]
    partial void LogSettleFailed(Guid lotteryId, long chatId, Exception ex);

}
