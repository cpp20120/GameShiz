using System.Globalization;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;
using BotFramework.Sdk.Events.Meta;
using Games.Meta.Domain.Quests;
using Games.Meta.Domain.Seasons;
using Games.Meta.Infrastructure.Catalog;

namespace Games.Meta.Application.Effects;

internal sealed class QuestProgressAtomicEffectHandler(IQuestCatalog catalog)
    : AtomicEffectHandler<QuestProgressAtomicEffect>
{
    protected override async Task ApplyAsync(QuestProgressAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var season = await context.QuerySingleOrDefaultAsync<MetaSeason>(
            """
            SELECT id AS Id, name AS Name, starts_at AS StartsAt, ends_at AS EndsAt,
                   status AS Status, config::text AS ConfigJson
            FROM meta_seasons
            WHERE id = @seasonId
            FOR UPDATE
            """,
            new { effect.SeasonId }, ct);
        if (season is null)
        {
            context.SetOutput("updates", Array.Empty<QuestProgressUpdate>());
            return;
        }

        var progress = await context.QuerySingleOrDefaultAsync<QuestPlayerProgress>(
            """
            SELECT level AS Level, games_played AS GamesPlayed, total_staked AS TotalStaked
            FROM meta_season_players
            WHERE season_id = @seasonId AND chat_id = @chatId AND user_id = @userId
            """,
            new { effect.SeasonId, effect.ChatId, effect.UserId }, ct)
            ?? new QuestPlayerProgress(0, 0, 0);
        var updates = new List<QuestProgressUpdate>();
        foreach (var quest in catalog.Matching(season, effect.ChatId, effect.UserId, effect.Completion, progress))
        {
            var delta = JsonQuestCatalog.DeltaFor(quest, effect.Completion);
            var periodKey = JsonQuestCatalog.PeriodKey(quest, effect.Now);
            var row = await context.QuerySingleOrDefaultAsync<QuestProgressUpdate>(
                """
                INSERT INTO meta_player_quests
                    (quest_id, season_id, chat_id, user_id, period_key, progress, target, completed)
                VALUES
                    (@questId, @seasonId, @chatId, @userId, @periodKey,
                     LEAST(@target, @delta), @target, @delta >= @target)
                ON CONFLICT (quest_id, season_id, chat_id, user_id, period_key)
                DO UPDATE SET progress = LEAST(meta_player_quests.target, meta_player_quests.progress + @delta),
                              completed = LEAST(meta_player_quests.target, meta_player_quests.progress + @delta) >= meta_player_quests.target,
                              updated_at = now()
                WHERE meta_player_quests.claimed = false
                RETURNING quest_id AS QuestId, progress, target, completed
                """,
                new
                {
                    questId = quest.Id,
                    effect.SeasonId,
                    effect.ChatId,
                    effect.UserId,
                    periodKey,
                    target = quest.Target,
                    delta,
                }, ct);
            if (row is not null)
                updates.Add(row);
        }
        context.SetOutput("updates", updates);
    }
}
