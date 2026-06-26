// ─────────────────────────────────────────────────────────────────────────────
// DiceCubeBetStore — Dapper-backed persistence for pending cube bets. A bet
// is keyed by (user_id, chat_id) so the same user can have independent bets
// in multiple group chats.
// ─────────────────────────────────────────────────────────────────────────────

using Dapper;

namespace Games.DiceCube.Infrastructure.Persistence;

public interface IDiceCubeBetStore
{
    Task<DiceCubeBet?> FindAsync(long userId, long chatId, CancellationToken ct);
    Task<bool> InsertAsync(DiceCubeBet bet, CancellationToken ct);
    Task DeleteAsync(long userId, long chatId, CancellationToken ct);
}
