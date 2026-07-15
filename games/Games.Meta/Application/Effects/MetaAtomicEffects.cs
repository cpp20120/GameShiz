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

public sealed record QuestProgressAtomicEffect(
    long SeasonId,
    long ChatId,
    long UserId,
    GameCompletedMetaEvent Completion,
    DateTimeOffset Now) : IAtomicEffect;

public sealed record QuestClaimAtomicEffect(
    long SeasonId,
    long ChatId,
    long UserId,
    string DisplayName,
    string QuestId,
    DateTimeOffset Now) : IAtomicEffect;

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
            new { effect.SeasonId }, ct).ConfigureAwait(false);
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
            new { effect.SeasonId, effect.ChatId, effect.UserId }, ct).ConfigureAwait(false)
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
                }, ct).ConfigureAwait(false);
            if (row is not null)
                updates.Add(row);
        }
        context.SetOutput("updates", updates);
    }
}

internal sealed class QuestClaimAtomicEffectHandler(
    IQuestCatalog catalog) : AtomicEffectHandler<QuestClaimAtomicEffect>
{
    protected override async Task ApplyAsync(
        QuestClaimAtomicEffect effect,
        IAtomicEffectContext context,
        CancellationToken ct)
    {
        var season = await context.QuerySingleOrDefaultAsync<MetaSeason>(
            """
            SELECT id AS Id, name AS Name, starts_at AS StartsAt, ends_at AS EndsAt,
                   status AS Status, config::text AS ConfigJson
            FROM meta_seasons
            WHERE id = @seasonId
            FOR UPDATE
            """,
            new { seasonId = effect.SeasonId }, ct).ConfigureAwait(false);
        if (season is null)
        {
            context.SetOutput("result", null);
            return;
        }

        var progress = await context.QuerySingleOrDefaultAsync<QuestPlayerProgress>(
            """
            SELECT level AS Level, games_played AS GamesPlayed, total_staked AS TotalStaked
            FROM meta_season_players
            WHERE season_id = @seasonId AND chat_id = @chatId AND user_id = @userId
            """,
            new { seasonId = effect.SeasonId, effect.ChatId, effect.UserId }, ct).ConfigureAwait(false)
            ?? new QuestPlayerProgress(0, 0, 0);
        var quest = catalog.FindActive(
            season,
            effect.ChatId,
            effect.UserId,
            effect.QuestId,
            effect.Now,
            progress);
        if (quest is null)
        {
            context.SetOutput("result", null);
            return;
        }

        var periodKey = JsonQuestCatalog.PeriodKey(quest, effect.Now);
        var claimedQuestId = await context.QuerySingleOrDefaultAsync<string?>(
            """
            UPDATE meta_player_quests
            SET claimed = true, claimed_at = now(), updated_at = now()
            WHERE quest_id = @questId
              AND season_id = @seasonId
              AND chat_id = @chatId
              AND user_id = @userId
              AND period_key = @periodKey
              AND completed = true
              AND claimed = false
            RETURNING quest_id
            """,
            new
            {
                questId = quest.Id,
                effect.SeasonId,
                effect.ChatId,
                effect.UserId,
                periodKey,
            }, ct).ConfigureAwait(false);

        var result = new QuestClaimResult(
            quest.Id,
            quest.Title,
            quest.RewardXp,
            quest.RewardCoins,
            claimedQuestId is not null);
        if (!result.Claimed)
        {
            context.SetOutput("result", result);
            return;
        }

        var wallet = context.Wallet ?? throw new InvalidOperationException("Wallet boundary is not configured.");
        await wallet.EnsureUserAsync(effect.UserId, effect.ChatId, effect.DisplayName, ct).ConfigureAwait(false);
        if (result.RewardCoins > 0)
        {
            var amount = checked((int)Math.Min(int.MaxValue, result.RewardCoins));
            var walletResult = await wallet.ApplyBatchAsync(
                effect.UserId,
                effect.ChatId,
                [new WalletBatchEffect(WalletBatchEffectKind.Credit, amount, "quest.reward")],
                $"quest:reward:{effect.SeasonId}:{effect.ChatId}:{effect.UserId}:{result.QuestId}",
                ct).ConfigureAwait(false);
            if (!walletResult.Applied)
                throw new InvalidOperationException("Quest reward wallet rejected the credit.");
        }

        if (result.RewardXp > 0)
            await AddXpAsync(context, effect, result.RewardXp, ct).ConfigureAwait(false);

        await context.ExecuteAsync(
            """
            INSERT INTO meta_event_log
                (event_type, aggregate_type, aggregate_id, season_id, chat_id, user_id, payload)
            VALUES ('quest.claimed', 'player', @aggregateId, @seasonId, @chatId, @userId, CAST(@payload AS jsonb))
            """,
            new
            {
                aggregateId = $"{effect.SeasonId}:{effect.ChatId}:{effect.UserId}",
                effect.SeasonId,
                effect.ChatId,
                effect.UserId,
                payload = System.Text.Json.JsonSerializer.Serialize(new
                {
                    result.QuestId,
                    result.Title,
                    result.RewardXp,
                    result.RewardCoins,
                    effect.DisplayName,
                }),
            }, ct).ConfigureAwait(false);

        context.SetOutput("result", result);
    }

    private static async Task AddXpAsync(
        IAtomicEffectContext context,
        QuestClaimAtomicEffect effect,
        long xpDelta,
        CancellationToken ct)
    {
        var configJson = await context.QuerySingleOrDefaultAsync<string?>(
            "SELECT config::text FROM meta_seasons WHERE id = @seasonId",
            new { effect.SeasonId }, ct).ConfigureAwait(false);
        var progression = SeasonProgressionConfig.FromJson(configJson);
        var player = await context.QuerySingleOrDefaultAsync<SeasonPlayer>(
            """
            INSERT INTO meta_season_players (season_id, chat_id, user_id, display_name, xp, level)
            VALUES (@seasonId, @chatId, @userId, @displayName, @xpDelta, @level)
            ON CONFLICT (season_id, chat_id, user_id)
            DO UPDATE SET display_name = EXCLUDED.display_name,
                          xp = meta_season_players.xp + @xpDelta,
                          updated_at = now()
            RETURNING season_id AS SeasonId, chat_id AS ChatId, user_id AS UserId,
                      display_name AS DisplayName, xp, level, rating,
                      games_played AS GamesPlayed, wins, losses,
                      total_staked AS TotalStaked, total_payout AS TotalPayout,
                      updated_at AS UpdatedAt
            """,
            new
            {
                effect.SeasonId,
                effect.ChatId,
                effect.UserId,
                displayName = effect.DisplayName,
                xpDelta = Math.Max(0, xpDelta),
                level = progression.LevelForXp(Math.Max(0, xpDelta)),
            }, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Quest XP update did not return a player.");
        var level = progression.LevelForXp(player.Xp);
        if (level != player.Level)
            await context.ExecuteAsync(
                "UPDATE meta_season_players SET level = @level, updated_at = now() WHERE season_id = @seasonId AND chat_id = @chatId AND user_id = @userId",
                new { effect.SeasonId, effect.ChatId, effect.UserId, level }, ct).ConfigureAwait(false);
    }

}
