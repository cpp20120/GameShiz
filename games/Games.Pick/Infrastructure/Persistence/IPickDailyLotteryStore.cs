// ─────────────────────────────────────────────────────────────────────────────
// PickDailyLotteryStore — Dapper queries for the per-chat daily lottery.
//
// Tables:
//   pick_daily_lottery          (chat_id, day_local) UNIQUE, status state machine
//   pick_daily_lottery_tickets  one row per purchased ticket (FK + cascade)
//
// Race-safe ergonomics:
//   • GetOrCreateOpenAsync uses INSERT ... ON CONFLICT DO NOTHING + SELECT
//     so the first concurrent caller wins the insert and the rest just read.
//   • Settle uses UPDATE ... WHERE status='open' so a manually-cancelled row
//     can't double-settle.
// ─────────────────────────────────────────────────────────────────────────────

using Dapper;

namespace Games.Pick.Infrastructure.Persistence;

public interface IPickDailyLotteryStore
{
    Task<PickDailyLotteryRow> GetOrCreateOpenAsync(
        long chatId, DateOnly dayLocal, int ticketPrice, DateTime deadlineUtc, CancellationToken ct);

    Task<PickDailyLotteryRow?> FindOpenByChatAsync(long chatId, DateOnly dayLocal, CancellationToken ct);

    Task<int> InsertTicketsAsync(
        Guid lotteryId, long userId, string displayName, int count, int pricePaid, CancellationToken ct);

    Task<int> CountTicketsAsync(Guid lotteryId, CancellationToken ct);

    Task<int> CountUserTicketsAsync(Guid lotteryId, long userId, CancellationToken ct);

    Task<int> CountDistinctUsersAsync(Guid lotteryId, CancellationToken ct);

    Task<IReadOnlyList<PickDailyTicketSummary>> ListUserTicketCountsAsync(
        Guid lotteryId, CancellationToken ct);

    Task<IReadOnlyList<PickDailyLotteryRow>> ListExpiredOpenAsync(int limit, CancellationToken ct);

    /// <summary>Picks a uniformly-random ticket and returns its owner. Null if 0 tickets.</summary>
    Task<(long UserId, string DisplayName)?> PickRandomWinnerAsync(Guid lotteryId, CancellationToken ct);

    Task<bool> MarkSettledAsync(Guid id, long winnerId, string winnerName,
        int ticketCount, int potTotal, int payout, int fee, CancellationToken ct);

    Task<bool> MarkCancelledAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<PickDailyLotteryRow>> ListHistoryAsync(long chatId, int limit, CancellationToken ct);
}
