using BotFramework.Host;
using Dapper;

namespace Games.Meta.Infrastructure.History;

public sealed class MetaReconstructionStore(INpgsqlConnectionFactory connections) : IMetaReconstructionStore
{
    public async Task<MetaReconstructionSummary> GetSummaryAsync(CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);

        const string sql = """
            SELECT
                (SELECT count(*)::bigint FROM meta_event_log WHERE event_type = 'game.completed') AS GameCompletedEvents,
                (SELECT count(*)::bigint FROM meta_event_log WHERE event_type = 'achievement.unlocked') AS AchievementEvents,
                (SELECT count(*)::bigint FROM (
                    SELECT DISTINCT season_id, chat_id, user_id
                    FROM meta_event_log
                    WHERE event_type = 'game.completed' AND season_id IS NOT NULL AND chat_id IS NOT NULL AND user_id IS NOT NULL
                ) x) AS ReconstructablePlayers,
                (SELECT count(*)::bigint FROM (
                    SELECT DISTINCT season_id, chat_id, user_id, payload->>'achievementId'
                    FROM meta_event_log
                    WHERE event_type = 'achievement.unlocked' AND payload ? 'achievementId'
                ) x) AS ReconstructableAchievements,
                (SELECT count(*)::bigint FROM meta_season_players) AS CurrentPlayers,
                (SELECT count(*)::bigint FROM meta_player_achievements) AS CurrentAchievements
            """;

        return await conn.QuerySingleAsync<MetaReconstructionSummary>(new CommandDefinition(sql, cancellationToken: ct));
    }

    public async Task<MetaReconstructionResult> ReconstructCoreAsync(CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await EnsureSchemaAsync(conn, ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        var beforePlayers = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*)::bigint FROM meta_season_players", transaction: tx, cancellationToken: ct));
        var beforeAchievements = await conn.ExecuteScalarAsync<long>(new CommandDefinition(
            "SELECT count(*)::bigint FROM meta_player_achievements", transaction: tx, cancellationToken: ct));

        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM meta_player_achievements", transaction: tx, cancellationToken: ct));
        await conn.ExecuteAsync(new CommandDefinition("DELETE FROM meta_season_players", transaction: tx, cancellationToken: ct));

        const string playersSql = """
            WITH latest AS (
                SELECT DISTINCT ON (season_id, chat_id, user_id)
                       season_id,
                       chat_id,
                       user_id,
                       COALESCE(NULLIF(payload->>'displayName', ''), user_id::text) AS display_name,
                       GREATEST(0, COALESCE((payload->>'xp')::bigint, 0)) AS xp,
                       GREATEST(1, COALESCE((payload->>'level')::int, 1)) AS level,
                       GREATEST(0, COALESCE((payload->>'rating')::int, 1000)) AS rating,
                       GREATEST(0, COALESCE((payload->>'gamesPlayed')::int, 0)) AS games_played,
                       GREATEST(0, COALESCE((payload->>'wins')::int, 0)) AS wins,
                       GREATEST(0, COALESCE((payload->>'losses')::int, 0)) AS losses,
                       GREATEST(0, COALESCE((payload->>'stake')::bigint, 0)) AS event_stake,
                       GREATEST(0, COALESCE((payload->>'payout')::bigint, 0)) AS event_payout,
                       occurred_at
                FROM meta_event_log
                WHERE event_type = 'game.completed'
                  AND season_id IS NOT NULL
                  AND chat_id IS NOT NULL
                  AND user_id IS NOT NULL
                ORDER BY season_id, chat_id, user_id, id DESC
            ), totals AS (
                SELECT season_id,
                       chat_id,
                       user_id,
                       SUM(GREATEST(0, COALESCE((payload->>'stake')::bigint, 0))) AS total_staked,
                       SUM(GREATEST(0, COALESCE((payload->>'payout')::bigint, 0))) AS total_payout
                FROM meta_event_log
                WHERE event_type = 'game.completed'
                  AND season_id IS NOT NULL
                  AND chat_id IS NOT NULL
                  AND user_id IS NOT NULL
                GROUP BY season_id, chat_id, user_id
            )
            INSERT INTO meta_season_players (
                season_id, chat_id, user_id, display_name, xp, level, rating,
                games_played, wins, losses, total_staked, total_payout, created_at, updated_at
            )
            SELECT l.season_id,
                   l.chat_id,
                   l.user_id,
                   l.display_name,
                   l.xp,
                   l.level,
                   l.rating,
                   l.games_played,
                   l.wins,
                   l.losses,
                   COALESCE(t.total_staked, l.event_stake),
                   COALESCE(t.total_payout, l.event_payout),
                   now(),
                   l.occurred_at
            FROM latest l
            LEFT JOIN totals t ON t.season_id = l.season_id AND t.chat_id = l.chat_id AND t.user_id = l.user_id
            """;
        var insertedPlayers = await conn.ExecuteAsync(new CommandDefinition(playersSql, transaction: tx, cancellationToken: ct));

        const string achievementsSql = """
            INSERT INTO meta_player_achievements (achievement_id, season_id, chat_id, user_id, unlocked_at)
            SELECT DISTINCT ON (payload->>'achievementId', season_id, chat_id, user_id)
                   payload->>'achievementId' AS achievement_id,
                   season_id,
                   chat_id,
                   user_id,
                   occurred_at
            FROM meta_event_log
            WHERE event_type = 'achievement.unlocked'
              AND payload ? 'achievementId'
              AND season_id IS NOT NULL
              AND chat_id IS NOT NULL
              AND user_id IS NOT NULL
            ORDER BY payload->>'achievementId', season_id, chat_id, user_id, id ASC
            """;
        var insertedAchievements = await conn.ExecuteAsync(new CommandDefinition(achievementsSql, transaction: tx, cancellationToken: ct));

        await tx.CommitAsync(ct);
        return new MetaReconstructionResult(beforePlayers, beforeAchievements, insertedPlayers, insertedAchievements);
    }

    private static async Task EnsureSchemaAsync(System.Data.Common.DbConnection conn, CancellationToken ct)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS meta_event_log (
                id              BIGSERIAL    PRIMARY KEY,
                event_type      TEXT         NOT NULL,
                aggregate_type  TEXT         NOT NULL,
                aggregate_id    TEXT         NOT NULL,
                season_id       BIGINT       NULL,
                chat_id         BIGINT       NULL,
                user_id         BIGINT       NULL,
                payload         JSONB        NOT NULL DEFAULT '{}'::jsonb,
                occurred_at     TIMESTAMPTZ  NOT NULL DEFAULT now()
            );
            """;
        await conn.ExecuteAsync(new CommandDefinition(sql, cancellationToken: ct));
    }
}
