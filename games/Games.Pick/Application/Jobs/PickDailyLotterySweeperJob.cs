// ─────────────────────────────────────────────────────────────────────────────
// PickDailyLotterySweeperJob — Quartz command that drains daily lotteries
// whose deadline (next-local-midnight UTC instant) has passed and posts the
// outcome in the originating chat.
//
// Same pattern as PickLotterySweeperJob but on the daily table. Runs on a
// minute-ish cadence (configurable). One missed minute is harmless: rows
// stay in 'open' and the next tick picks them up.
// ─────────────────────────────────────────────────────────────────────────────

using Microsoft.Extensions.Options;
using BotFramework.Scheduling.Abstractions;

namespace Games.Pick.Application.Jobs;

public sealed partial class PickDailyLotterySweeperJob(
    IPickDailyLotteryStore store,
    IPickDailyLotteryService service,
    IPickAnnouncementPublisher announcements,
    IOptions<PickOptions> options,
    ILogger<PickDailyLotterySweeperJob> logger) : IRecurringScheduledCommand
{
    public string Key => "pick.daily.lottery.sweeper";
    public ScheduleDescriptor Schedule =>
        ScheduleDescriptor.Every(TimeSpan.FromSeconds(Math.Max(15, options.Value.Daily.SweeperIntervalSeconds)));

    public async Task ExecuteAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct)
    {
        try
        {
            var expired = await store.ListExpiredOpenAsync(limit: 32, ct);
            foreach (var row in expired)
            {
                try
                {
                    var settle = await service.SettleAsync(row, ct);
                    await announcements.PublishDailyAsync(settle, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { LogSettleFailed(row.Id, row.ChatId, ex); }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { LogTickFailed(ex); }
    }

    [LoggerMessage(EventId = 5971, Level = LogLevel.Error,
        Message = "pick.daily.sweeper.tick_failed")]
    partial void LogTickFailed(Exception ex);

    [LoggerMessage(EventId = 5972, Level = LogLevel.Warning,
        Message = "pick.daily.sweeper.settle_failed lottery={LotteryId} chat={ChatId}")]
    partial void LogSettleFailed(Guid lotteryId, long chatId, Exception ex);

}
