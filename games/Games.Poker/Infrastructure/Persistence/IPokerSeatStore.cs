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

public interface IPokerSeatStore
{
    Task<PokerSeat?> FindByUserAsync(long userId, CancellationToken ct);
    Task<PokerSeat?> FindByUserInTableAsync(long userId, string inviteCode, CancellationToken ct);
    Task<List<PokerSeat>> ListByTableAsync(string inviteCode, CancellationToken ct);
    Task<int> CountByTableAsync(string inviteCode, long exceptUserId, CancellationToken ct);
    Task<bool> AnyForUserAsync(long userId, CancellationToken ct);
    Task InsertAsync(PokerSeat seat, CancellationToken ct);
    Task UpdateAsync(PokerSeat seat, CancellationToken ct);
    Task DeleteAsync(string inviteCode, int position, CancellationToken ct);
    Task UpsertStateMessageAsync(long userId, int messageId, CancellationToken ct);
}
