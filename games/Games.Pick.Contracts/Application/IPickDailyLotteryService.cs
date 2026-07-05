// ─────────────────────────────────────────────────────────────────────────────
// PickDailyLotteryService — orchestrates the per-chat daily ticket pool.
//
// Day boundary:
//   The day is keyed by local-date in UTC + (hoursEastOfUtc) — the same
//   offset the dice-daily-limit system uses (configured under
//   Bot:TelegramDiceDailyLimit:TimezoneOffsetHours, default 7). PickDaily
//   options can override via TimezoneOffsetHoursOverride.
//
// Buy flow:
//   1. Compute today's local date and corresponding UTC deadline (midnight
//      of NEXT local day expressed in UTC).
//   2. GetOrCreate the lottery row for (chat, today). Idempotent + race-safe.
//   3. If the row's deadline has already passed (clock skew at midnight ±
//      a few seconds), bow out — the sweeper will draw it shortly. The user
//      can retry the next day.
//   4. EnsureUser, check balance ≥ count × ticket_price, debit total in one
//      shot, then INSERT N ticket rows. If the insert fails partway, refund.
//
// Settle flow (sweeper-driven):
//   • 0 tickets → MarkCancelled. No coins moved (nothing was paid in).
//   • ≥1 tickets → PickRandomWinner (uniform over all ticket rows), credit
//     pot − fee to winner, MarkSettled.
// ─────────────────────────────────────────────────────────────────────────────


namespace Games.Pick.Application.Services;

public interface IPickDailyLotteryService
{
    Task<DailyBuyResult> BuyAsync(
        long userId, string displayName, long chatId, int count, CancellationToken ct);

    Task<DailyInfoSnapshot?> InfoAsync(long chatId, long viewerId, CancellationToken ct);

    /// <summary>Sweeper-driven draw or cancel for one expired pool.</summary>
    Task<DailySettleResult> SettleAsync(PickDailyLotteryRow row, CancellationToken ct);

    Task<IReadOnlyList<PickDailyLotteryRow>> HistoryAsync(long chatId, int limit, CancellationToken ct);

    /// <summary>Calendar date (in the configured offset) of the next-upcoming draw.</summary>
    DateOnly LocalToday();

    /// <summary>UTC instant of the next-upcoming local draw (cycle end).</summary>
    DateTime LocalNextDrawUtc();

    int OffsetHours { get; }

    /// <summary>Local hour-of-day at which the daily cycle closes (0..23).</summary>
    int DrawHourLocal { get; }
}
