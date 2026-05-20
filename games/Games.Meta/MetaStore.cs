using BotFramework.Host;
using Dapper;

namespace Games.Meta;

public interface IMetaStore
{
    Task<MetaSeason> GetOrCreateActiveSeasonAsync(CancellationToken ct);
    Task<SeasonPlayer> EnsurePlayerAsync(MetaSeason season, long chatId, long userId, string displayName, CancellationToken ct);
    Task<SeasonProfile> GetProfileAsync(long chatId, long userId, string displayName, CancellationToken ct);
    Task<IReadOnlyList<SeasonLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct);
}

public sealed class MetaStore(INpgsqlConnectionFactory connections) : IMetaStore
{
    private const string DefaultSeasonConfigJson = """
        {
          "xp": {
            "play": 5,
            "win": 25,
            "loss": 2,
            "stakeMultiplier": 0.01,
            "maxXpPerGame": 500
          },
          "rating": {
            "enabled": true,
            "start": 1000,
            "winDelta": 16,
            "lossDelta": -12
          },
          "quests": {
            "dailyEnabled": true,
            "weeklyEnabled": true
          },
          "achievements": {
            "enabled": true
          },
          "clans": {
            "enabled": true,
            "maxMembers": 20
          },
          "tournaments": {
            "enabled": true,
            "maxActivePerChat": 3
          },
          "risk": {
            "enabled": true,
            "largeWinMultiplierAlert": 20,
            "suspiciousStreakThreshold": 12
          }
        }
        """;

    public async Task<MetaSeason> GetOrCreateActiveSeasonAsync(CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);

        const string findSql = """
            SELECT id,
                   name,
                   starts_at AS StartsAt,
                   ends_at AS EndsAt,
                   status,
                   config::text AS ConfigJson
            FROM meta_seasons
            WHERE status = 'active'
            ORDER BY starts_at DESC
            LIMIT 1
            """;

        var existing = await conn.QuerySingleOrDefaultAsync<MetaSeason>(new CommandDefinition(
            findSql, transaction: tx, cancellationToken: ct));

        if (existing is not null)
        {
            await tx.CommitAsync(ct);
            return existing;
        }

        const string insertSql = """
            INSERT INTO meta_seasons (name, starts_at, ends_at, status, config)
            VALUES (
                @name,
                date_trunc('day', now()),
                date_trunc('day', now()) + interval '30 days',
                'active',
                CAST(@configJson AS jsonb)
            )
            RETURNING id,
                      name,
                      starts_at AS StartsAt,
                      ends_at AS EndsAt,
                      status,
                      config::text AS ConfigJson
            """;

        var created = await conn.QuerySingleAsync<MetaSeason>(new CommandDefinition(
            insertSql,
            new { name = "Season 1: Shizoid League", configJson = DefaultSeasonConfigJson },
            transaction: tx,
            cancellationToken: ct));

        await tx.CommitAsync(ct);
        return created;
    }

    public async Task<SeasonPlayer> EnsurePlayerAsync(
        MetaSeason season,
        long chatId,
        long userId,
        string displayName,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO meta_season_players (season_id, chat_id, user_id, display_name)
            VALUES (@seasonId, @chatId, @userId, @displayName)
            ON CONFLICT (season_id, chat_id, user_id)
            DO UPDATE SET display_name = EXCLUDED.display_name,
                          updated_at = now()
            RETURNING season_id AS SeasonId,
                      chat_id AS ChatId,
                      user_id AS UserId,
                      display_name AS DisplayName,
                      xp,
                      level,
                      rating,
                      games_played AS GamesPlayed,
                      wins,
                      losses,
                      total_staked AS TotalStaked,
                      total_payout AS TotalPayout,
                      updated_at AS UpdatedAt
            """;

        await using var conn = await connections.OpenAsync(ct);
        return await conn.QuerySingleAsync<SeasonPlayer>(new CommandDefinition(
            sql,
            new { seasonId = season.Id, chatId, userId, displayName },
            cancellationToken: ct));
    }

    public async Task<SeasonProfile> GetProfileAsync(
        long chatId,
        long userId,
        string displayName,
        CancellationToken ct)
    {
        var season = await GetOrCreateActiveSeasonAsync(ct);
        var player = await EnsurePlayerAsync(season, chatId, userId, displayName, ct);
        var floor = XpForLevel(player.Level);
        var next = XpForLevel(player.Level + 1);
        return new SeasonProfile(season, player, DivisionForRating(player.Rating), next, floor);
    }

    public async Task<IReadOnlyList<SeasonLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct)
    {
        var season = await GetOrCreateActiveSeasonAsync(ct);
        const string sql = """
            SELECT row_number() OVER (ORDER BY xp DESC, rating DESC, user_id ASC)::int AS Place,
                   user_id AS UserId,
                   display_name AS DisplayName,
                   xp,
                   level,
                   rating,
                   games_played AS GamesPlayed,
                   wins,
                   losses
            FROM meta_season_players
            WHERE season_id = @seasonId AND chat_id = @chatId
            ORDER BY xp DESC, rating DESC, user_id ASC
            LIMIT @limit
            """;

        await using var conn = await connections.OpenAsync(ct);
        var rows = await conn.QueryAsync<SeasonLeaderboardEntry>(new CommandDefinition(
            sql,
            new { seasonId = season.Id, chatId, limit = Math.Clamp(limit, 1, 100) },
            cancellationToken: ct));
        return rows.ToList();
    }

    private static long XpForLevel(int level)
    {
        var normalized = Math.Max(1, level) - 1;
        return 100L * normalized * normalized;
    }

    private static string DivisionForRating(int rating) => rating switch
    {
        < 900 => "Bronze",
        < 1100 => "Silver",
        < 1300 => "Gold",
        < 1500 => "Platinum",
        < 1800 => "Diamond",
        _ => "Shizoid"
    };
}
