using BotFramework.Host;
using Dapper;

namespace Games.Meta;

public sealed class QuestStore(INpgsqlConnectionFactory connections, IQuestCatalog questCatalog) : IQuestStore
{
    public async Task<IReadOnlyList<QuestProgressUpdate>> ApplyGameCompletedAsync(
        MetaSeason season,
        long chatId,
        long userId,
        GameCompletedMetaEvent ev,
        CancellationToken ct)
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(ev.OccurredAt);
        var updates = new List<QuestProgressUpdate>();

        await using var conn = await connections.OpenAsync(ct);
        var playerProgress = await LoadPlayerProgressAsync(conn, season, chatId, userId, ct);
        foreach (var quest in questCatalog.Matching(season, chatId, userId, ev, playerProgress))
        {
            var delta = JsonQuestCatalog.DeltaFor(quest, ev);
            var periodKey = JsonQuestCatalog.PeriodKey(quest, now);

            const string sql = """
                INSERT INTO meta_player_quests (
                    quest_id,
                    season_id,
                    chat_id,
                    user_id,
                    period_key,
                    progress,
                    target,
                    completed
                )
                VALUES (
                    @questId,
                    @seasonId,
                    @chatId,
                    @userId,
                    @periodKey,
                    LEAST(@target, @delta),
                    @target,
                    @delta >= @target
                )
                ON CONFLICT (quest_id, season_id, chat_id, user_id, period_key)
                DO UPDATE SET progress = LEAST(meta_player_quests.target, meta_player_quests.progress + @delta),
                              completed = LEAST(meta_player_quests.target, meta_player_quests.progress + @delta) >= meta_player_quests.target,
                              updated_at = now()
                WHERE meta_player_quests.claimed = false
                RETURNING quest_id AS QuestId,
                          progress,
                          target,
                          completed
                """;

            var row = await conn.QuerySingleOrDefaultAsync<QuestProgressUpdate>(new CommandDefinition(
                sql,
                new
                {
                    questId = quest.Id,
                    seasonId = season.Id,
                    chatId,
                    userId,
                    periodKey,
                    target = quest.Target,
                    delta,
                },
                cancellationToken: ct));

            if (row is not null)
                updates.Add(row);
        }

        return updates;
    }

    public async Task<IReadOnlyList<PlayerQuestView>> GetQuestsAsync(
        MetaSeason season,
        long chatId,
        long userId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var playerProgress = await LoadPlayerProgressAsync(conn, season, chatId, userId, ct);
        var activeQuests = questCatalog.ActiveFor(season, chatId, userId, now, playerProgress);
        if (activeQuests.Count == 0)
            return [];

        var periodKeys = activeQuests
            .Select(q => JsonQuestCatalog.PeriodKey(q, now))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var questIds = activeQuests.Select(q => q.Id).ToArray();

        const string sql = """
            SELECT quest_id AS QuestId,
                   period_key AS PeriodKey,
                   progress,
                   target,
                   completed,
                   claimed
            FROM meta_player_quests
            WHERE season_id = @seasonId
              AND chat_id = @chatId
              AND user_id = @userId
              AND period_key = ANY(@periodKeys)
              AND quest_id = ANY(@questIds)
            """;

        var rows = await conn.QueryAsync<QuestProgressRow>(new CommandDefinition(
            sql,
            new { seasonId = season.Id, chatId, userId, periodKeys, questIds },
            cancellationToken: ct));

        var map = rows.ToDictionary(x => (x.QuestId, x.PeriodKey), x => x);
        return activeQuests.Select(q =>
        {
            var periodKey = JsonQuestCatalog.PeriodKey(q, now);
            map.TryGetValue((q.Id, periodKey), out var row);
            return new PlayerQuestView(
                q.Id,
                q.Title,
                q.Description,
                q.Period,
                row?.Progress ?? 0,
                q.Target,
                row?.Completed ?? false,
                row?.Claimed ?? false,
                q.RewardXp,
                q.RewardCoins);
        }).ToList();
    }

    public async Task<QuestClaimResult?> TryMarkClaimedAsync(
        MetaSeason season,
        long chatId,
        long userId,
        string questId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        await using var conn = await connections.OpenAsync(ct);
        var playerProgress = await LoadPlayerProgressAsync(conn, season, chatId, userId, ct);
        var quest = questCatalog.FindActive(season, chatId, userId, questId, now, playerProgress);
        if (quest is null) return null;

        var periodKey = JsonQuestCatalog.PeriodKey(quest, now);
        const string sql = """
            UPDATE meta_player_quests
            SET claimed = true,
                claimed_at = now(),
                updated_at = now()
            WHERE quest_id = @questId
              AND season_id = @seasonId
              AND chat_id = @chatId
              AND user_id = @userId
              AND period_key = @periodKey
              AND completed = true
              AND claimed = false
            RETURNING quest_id
            """;

        var claimedQuestId = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            sql,
            new { questId = quest.Id, seasonId = season.Id, chatId, userId, periodKey },
            cancellationToken: ct));

        return claimedQuestId is null
            ? new QuestClaimResult(quest.Id, quest.Title, quest.RewardXp, quest.RewardCoins, false)
            : new QuestClaimResult(quest.Id, quest.Title, quest.RewardXp, quest.RewardCoins, true);
    }

    private static async Task<QuestPlayerProgress> LoadPlayerProgressAsync(
        System.Data.IDbConnection conn,
        MetaSeason season,
        long chatId,
        long userId,
        CancellationToken ct)
    {
        const string sql = """
            SELECT level AS Level,
                   games_played AS GamesPlayed,
                   total_staked AS TotalStaked
            FROM meta_season_players
            WHERE season_id = @seasonId
              AND chat_id = @chatId
              AND user_id = @userId
            """;

        return await conn.QuerySingleOrDefaultAsync<QuestPlayerProgress>(new CommandDefinition(
            sql,
            new { seasonId = season.Id, chatId, userId },
            cancellationToken: ct)) ?? new QuestPlayerProgress(0, 0, 0);
    }

}
