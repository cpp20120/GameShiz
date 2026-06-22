// ─────────────────────────────────────────────────────────────────────────────
// PokerStores — Dapper-backed access for poker_tables + poker_seats. The
// domain mutates PokerTable/PokerSeat instances in place, so updates
// overwrite every field from the in-memory object.
//
// Concurrency: PokerService serializes all mutating operations behind a
// process-local SemaphoreSlim (same shape as the monolith). Distributed hosts
// would require a proper per-table lock or optimistic concurrency — out of
// scope for this port.
// ─────────────────────────────────────────────────────────────────────────────

using BotFramework.Host;
using Dapper;

namespace Games.Poker.Infrastructure.Persistence;

public interface IPokerTableStore
{
    Task<PokerTable?> FindAsync(string inviteCode, CancellationToken ct);
    Task<PokerTable?> FindOpenByChatAsync(long chatId, CancellationToken ct);
    Task<bool> CodeExistsAsync(string inviteCode, CancellationToken ct);
    Task InsertAsync(PokerTable table, CancellationToken ct);
    Task UpdateAsync(PokerTable table, CancellationToken ct);
    Task UpsertStateMessageAsync(string inviteCode, int messageId, CancellationToken ct);
    Task<IReadOnlyList<string>> ListStuckCodesAsync(long cutoffMs, CancellationToken ct);
}
