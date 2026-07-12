// ─────────────────────────────────────────────────────────────────────────────
// DiceCubeBetStore — Dapper-backed persistence for pending cube bets. A bet
// is keyed by (user_id, chat_id) so the same user can have independent bets
// in multiple group chats.
// ─────────────────────────────────────────────────────────────────────────────

using Dapper;

namespace Games.DiceCube.Infrastructure.Persistence;

public sealed class DiceCubeBetStore(INpgsqlConnectionFactory connections) : IDiceCubeBetStore
{
    public async Task<DiceCubeBet?> FindAsync(long userId, long chatId, CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        return await conn.QuerySingleOrDefaultAsync<DiceCubeBet>(new CommandDefinition("""
            SELECT user_id AS UserId, chat_id AS ChatId, amount AS Amount, created_at AS CreatedAt,
                   mult4 AS Mult4, mult5 AS Mult5, mult6 AS Mult6
            FROM dicecube_bets WHERE user_id = @userId AND chat_id = @chatId
            """,
            new { userId, chatId },
            cancellationToken: ct));
    }

}
