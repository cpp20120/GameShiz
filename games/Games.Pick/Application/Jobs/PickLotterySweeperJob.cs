// ─────────────────────────────────────────────────────────────────────────────
// PickLotterySweeperJob — IBackgroundJob that periodically settles expired
// /picklottery pools and posts the outcome to the originating chat.
//
// Runs at PickLotteryOptions.SweeperIntervalSeconds. Each tick:
//   1. ListExpiredOpenAsync(N) — small batch so we don't choke a busy chat.
//   2. For each row: PickLotteryService.SettleAsync (it decides whether to
//      draw or cancel based on entrant count).
//   3. Post a result/refund message to chat.id with parseMode=HTML.
//
// Failure of any one row never stops the loop — the next tick retries
// remaining expired pools because we only mark them settled/cancelled
// after the in-DB transition succeeds.
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.Extensions.Options;

namespace Games.Pick.Application.Jobs;

public sealed partial class PickLotterySweeperJob(
    IPickLotteryStore store,
    IPickLotteryService service,
    IPickAnnouncementPublisher announcements,
    IOptions<PickOptions> options,
    ILogger<PickLotterySweeperJob> logger) : IBackgroundJob
{
    public string Name => "pick.lottery.sweeper";

    public async Task RunAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.Lottery.SweeperIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var expired = await store.ListExpiredOpenAsync(limit: 16, stoppingToken);
                foreach (var row in expired)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    try
                    {
                        var result = await service.SettleAsync(row, stoppingToken);
                        await announcements.PublishLotteryAsync(result, stoppingToken);
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

    [LoggerMessage(EventId = 5931, Level = LogLevel.Error,
        Message = "pick.lottery.sweeper.tick_failed")]
    partial void LogTickFailed(Exception ex);

    [LoggerMessage(EventId = 5932, Level = LogLevel.Warning,
        Message = "pick.lottery.sweeper.settle_failed lottery={LotteryId} chat={ChatId}")]
    partial void LogSettleFailed(Guid lotteryId, long chatId, Exception ex);

}
