// ─────────────────────────────────────────────────────────────────────────────
// ChatsStore — read-only query layer for the admin /chats command.
//
// Joins the framework-owned `known_chats` table (populated by
// KnownChatsMiddleware on every update) with the `users` table so the admin
// list shows engagement at a glance: how many wallets that chat has and the
// summed balance per chat. No mutation methods live here — the migrations
// for `known_chats` itself ship inside the framework.
// ─────────────────────────────────────────────────────────────────────────────

using Dapper;

namespace Games.Admin.Infrastructure.Persistence;

public sealed class ChatsStore(INpgsqlConnectionFactory connections, IWalletReadService wallets) : IChatsStore
{
    public async Task<IReadOnlyList<KnownChatRow>> ListChatsAsync(
        string? typeFilter, int limit, CancellationToken ct)
    {
        const string sql = """
            SELECT kc.chat_id        AS ChatId,
                   kc.chat_type      AS ChatType,
                   kc.title          AS Title,
                   kc.username       AS Username,
                   kc.first_seen_at  AS FirstSeenAt,
                   kc.last_seen_at   AS LastSeenAt,
                   0 AS UserCount,
                   0::BIGINT AS TotalCoins
            FROM known_chats kc
            WHERE (@typeFilter IS NULL OR kc.chat_type = ANY(@types))
            ORDER BY kc.last_seen_at DESC
            LIMIT NULLIF(@limit, 0)
            """;

        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<KnownChatRow>(new CommandDefinition(
            sql, new { typeFilter, types = ResolveTypes(typeFilter), limit }, cancellationToken: ct));
        var aggregates = (await wallets.ListAsync(ct))
            .GroupBy(x => x.BalanceScopeId)
            .ToDictionary(x => x.Key, x => (Count: x.Count(), Coins: x.Sum(a => (long)a.Coins)));
        return rows.Select(row => aggregates.TryGetValue(row.ChatId, out var value)
            ? row with { UserCount = value.Count, TotalCoins = value.Coins }
            : row).ToList();
    }

    public async Task<int> CountChatsAsync(string? typeFilter, CancellationToken ct)
    {
        const string sql = """
            SELECT count(*)::INT
            FROM known_chats
            WHERE (@typeFilter IS NULL OR chat_type = ANY(@types))
            """;

        await using var conn = await connections.OpenAsync(ct);
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition(
            sql, new { typeFilter, types = ResolveTypes(typeFilter) }, cancellationToken: ct));
    }

    private static string[] ResolveTypes(string? typeFilter) => typeFilter switch
    {
        null => [],
        "private" => ["private"],
        "group" => ["group", "supergroup"],
        "channel" => ["channel"],
        _ => [typeFilter],
    };
}
