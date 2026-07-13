// ─────────────────────────────────────────────────────────────────────────────
// PickLotterySweeperJob — Quartz command that periodically settles expired
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
using BotFramework.Scheduling.Abstractions;

namespace Games.Pick.Application.Jobs;

public sealed partial class PickLotterySweeperJob(
    IPickLotteryStore store,
    IPickLotteryService service,
    IPickAnnouncementPublisher announcements,
    IOptions<PickOptions> options,
    ILogger<PickLotterySweeperJob> logger) : IRecurringScheduledCommand
{
    public string Key => "pick.lottery.sweeper";
    public ScheduleDescriptor Schedule =>
        ScheduleDescriptor.Every(TimeSpan.FromSeconds(Math.Max(5, options.Value.Lottery.SweeperIntervalSeconds)));

    public async Task ExecuteAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        try
        {
            var expired = await store.ListExpiredOpenAsync(limit: 16, ct);
            foreach (var row in expired)
            {
                try
                {
                    var result = await service.SettleAsync(row, ct);
                    await announcements.PublishLotteryAsync(result, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { LogSettleFailed(row.Id, row.ChatId, ex); }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { LogTickFailed(ex); }
    }

    [LoggerMessage(EventId = 5931, Level = LogLevel.Error,
        Message = "pick.lottery.sweeper.tick_failed")]
    partial void LogTickFailed(Exception ex);

    [LoggerMessage(EventId = 5932, Level = LogLevel.Warning,
        Message = "pick.lottery.sweeper.settle_failed lottery={LotteryId} chat={ChatId}")]
    partial void LogSettleFailed(Guid lotteryId, long chatId, Exception ex);

}
