// ─────────────────────────────────────────────────────────────────────────────
// PickLotteryStore — Dapper queries for the /picklottery game.
//
// Locking strategy:
//   • Open / join writes go through a transaction with FOR UPDATE on the
//     pick_lottery row to keep entry counts and refund states consistent.
//   • The unique partial index ux_pick_lottery_open_per_chat is the canonical
//     guarantee that there is at most one open pool per chat — race-free at
//     the DB level, no application-side locking required.
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Host;
using Dapper;

namespace Games.Pick.Infrastructure.Persistence;

public interface IPickLotteryStore
{
    Task<(LotteryOpenError Err, PickLotteryRow? Row)> InsertOpenAsync(
        Guid id, long chatId, long openerId, string openerName,
        int stake, DateTime deadlineUtc, CancellationToken ct);

    Task<PickLotteryRow?> FindOpenByChatAsync(long chatId, CancellationToken ct);

    Task<PickLotteryRow?> FindByIdAsync(Guid id, CancellationToken ct);

    Task<(LotteryJoinError Err, PickLotteryRow? Row)> AddEntryAsync(
        long chatId, long userId, string displayName, CancellationToken ct);

    Task<IReadOnlyList<PickLotteryEntryRow>> ListEntriesAsync(Guid lotteryId, CancellationToken ct);

    /// <summary>Returns up to N expired open pools for the sweeper.</summary>
    Task<IReadOnlyList<PickLotteryRow>> ListExpiredOpenAsync(int limit, CancellationToken ct);

    /// <summary>Atomically transitions an open pool to settled with the chosen winner.</summary>
    Task<bool> MarkSettledAsync(
        Guid id, long winnerId, string winnerName,
        int potTotal, int payout, int fee, CancellationToken ct);

    /// <summary>Atomically transitions an open pool to cancelled (no winner).</summary>
    Task<bool> MarkCancelledAsync(Guid id, CancellationToken ct);
}
