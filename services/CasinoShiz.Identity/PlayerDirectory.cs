using BotFramework.Contracts.Identity;
using BotFramework.Host.Persistence.Connections;
using Dapper;

namespace CasinoShiz.Identity;

public sealed class PlayerDirectory(INpgsqlConnectionFactory connections) : IPlayerDirectory
{
    public async Task UpsertAsync(PlayerIdentity identity, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT INTO player_identities
                (telegram_user_id, display_name, username, first_seen_at, last_seen_at)
            VALUES (@UserId, @DisplayName, @Username, @FirstSeenAt, @LastSeenAt)
            ON CONFLICT (telegram_user_id) DO UPDATE SET
                display_name = EXCLUDED.display_name,
                username = COALESCE(EXCLUDED.username, player_identities.username),
                last_seen_at = GREATEST(player_identities.last_seen_at, EXCLUDED.last_seen_at)
            """, identity, cancellationToken: ct));
    }

    public async Task<PlayerIdentity?> GetAsync(long userId, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<PlayerIdentity>(new CommandDefinition("""
            SELECT telegram_user_id AS UserId, display_name AS DisplayName, username AS Username,
                   first_seen_at AS FirstSeenAt, last_seen_at AS LastSeenAt
            FROM player_identities WHERE telegram_user_id = @userId
            """, new { userId }, cancellationToken: ct));
    }

    public async Task<PlayerIdentity?> FindByUsernameAsync(string username, CancellationToken ct)
    {
        await using var connection = await connections.OpenAsync(ct);
        return await connection.QuerySingleOrDefaultAsync<PlayerIdentity>(new CommandDefinition("""
            SELECT telegram_user_id AS UserId, display_name AS DisplayName, username AS Username,
                   first_seen_at AS FirstSeenAt, last_seen_at AS LastSeenAt
            FROM player_identities
            WHERE lower(username) = lower(@username)
            ORDER BY last_seen_at DESC LIMIT 1
            """, new { username = username.TrimStart('@') }, cancellationToken: ct));
    }
}
