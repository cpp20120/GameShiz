using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Execution;
using Dapper;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Models;
using Games.Meta.Domain.Seasons;

namespace Games.Meta.Application.Effects;

public sealed record SeasonPlayerRewardsAtomicEffect(long SeasonId) : IAtomicEffect;
public sealed record SeasonClanRewardsAtomicEffect(long SeasonId) : IAtomicEffect;

internal abstract class SeasonRewardsAtomicEffectHandler<TEffect> : AtomicEffectHandler<TEffect>
    where TEffect : class, IAtomicEffect
{
    protected static Task<string?> ConfigAsync(IAtomicEffectContext context, long seasonId, CancellationToken ct) =>
        context.QuerySingleOrDefaultAsync<string?>("SELECT config::text FROM meta_seasons WHERE id = @seasonId FOR UPDATE", new { seasonId }, ct);

    protected async Task CreditAsync(
        IAtomicEffectContext context,
        long userId,
        long chatId,
        string displayName,
        int amount,
        string reason,
        string operationId,
        CancellationToken ct)
    {
        if (amount <= 0) return;
        var wallet = context.Wallet ?? throw new InvalidOperationException("Wallet boundary is not configured.");
        await wallet.EnsureUserAsync(userId, chatId, displayName, ct).ConfigureAwait(false);
        var result = await wallet.ApplyBatchAsync(
            userId,
            chatId,
            [new WalletBatchEffect(WalletBatchEffectKind.Credit, amount, reason)],
            operationId,
            ct).ConfigureAwait(false);
        if (!result.Applied)
            throw new InvalidOperationException("Season reward wallet rejected the credit.");
    }

    protected static Task AppendHistoryAsync(IAtomicEffectContext context, long seasonId, string eventType, object payload, CancellationToken ct) =>
        context.ExecuteAsync(
            "INSERT INTO meta_event_log (event_type, aggregate_type, aggregate_id, season_id, payload) VALUES (@eventType, 'season', @aggregateId, @seasonId, CAST(@payload AS jsonb))",
            new { eventType, aggregateId = seasonId.ToString(CultureInfo.InvariantCulture), seasonId, payload = JsonSerializer.Serialize(payload) }, ct);

}

internal sealed class SeasonPlayerRewardsAtomicEffectHandler : SeasonRewardsAtomicEffectHandler<SeasonPlayerRewardsAtomicEffect>
{
    protected override async Task ApplyAsync(SeasonPlayerRewardsAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var configJson = await ConfigAsync(context, effect.SeasonId, ct).ConfigureAwait(false);
        if (configJson is null)
        {
            context.SetOutput("result", new SeasonRewardProcessResult(0, []));
            return;
        }
        var rewards = SeasonRewardsConfig.FromJson(configJson);
        var winners = await context.QueryAsync<PlayerSeasonRewardWinner>(
            """
            SELECT row_number() OVER (ORDER BY xp DESC, rating DESC, user_id ASC)::int AS Place,
                   chat_id AS ChatId, user_id AS UserId, display_name AS DisplayName
            FROM meta_season_players
            WHERE season_id = @seasonId
            ORDER BY xp DESC, rating DESC, user_id ASC
            LIMIT 10
            """,
            new { effect.SeasonId }, ct).ConfigureAwait(false);
        var rows = new List<SeasonRewardPaidRow>();
        foreach (var winner in winners)
        {
            var amount = rewards.PlayerRewardForPlace(winner.Place);
            if (amount <= 0) continue;
            await CreditAsync(context, winner.UserId, winner.ChatId, winner.DisplayName, amount, "season.reward", $"season:reward:{effect.SeasonId}:{winner.Place}:{winner.ChatId}:{winner.UserId}", ct).ConfigureAwait(false);
            rows.Add(new SeasonRewardPaidRow(winner.Place, winner.ChatId, winner.UserId, winner.DisplayName, amount));
        }
        var result = new SeasonRewardProcessResult(rows.Count, rows);
        await AppendHistoryAsync(context, effect.SeasonId, "season.reward_paid", new { effect.SeasonId, paid = rows.Count, winners = rows }, ct).ConfigureAwait(false);
        context.SetOutput("result", result);
    }
}

internal sealed class SeasonClanRewardsAtomicEffectHandler : SeasonRewardsAtomicEffectHandler<SeasonClanRewardsAtomicEffect>
{
    protected override async Task ApplyAsync(SeasonClanRewardsAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var configJson = await ConfigAsync(context, effect.SeasonId, ct).ConfigureAwait(false);
        if (configJson is null)
        {
            context.SetOutput("result", new SeasonRewardProcessResult(0, []));
            return;
        }
        var rewards = SeasonRewardsConfig.FromJson(configJson);
        var winners = await context.QueryAsync<ClanSeasonRewardWinner>(
            """
            SELECT row_number() OVER (ORDER BY sc.xp DESC, sc.rating DESC, sc.clan_id ASC)::int AS Place,
                   sc.chat_id AS ChatId, sc.clan_id AS ClanId, c.name AS ClanName, c.tag AS ClanTag,
                   c.owner_user_id AS OwnerUserId,
                   COALESCE(m.display_name, c.owner_user_id::text) AS OwnerDisplayName
            FROM meta_season_clans sc
            JOIN meta_clans c ON c.id = sc.clan_id
            LEFT JOIN meta_clan_members m ON m.clan_id = c.id AND m.user_id = c.owner_user_id
            WHERE sc.season_id = @seasonId
            ORDER BY sc.xp DESC, sc.rating DESC, sc.clan_id ASC
            LIMIT 10
            """,
            new { effect.SeasonId }, ct).ConfigureAwait(false);
        var rows = new List<SeasonRewardPaidRow>();
        foreach (var winner in winners)
        {
            var amount = rewards.ClanRewardForPlace(winner.Place);
            if (amount <= 0) continue;
            await CreditAsync(context, winner.OwnerUserId, winner.ChatId, winner.OwnerDisplayName, amount, "season.clan_reward", $"season:clan-reward:{effect.SeasonId}:{winner.Place}:{winner.ChatId}:{winner.ClanId}:{winner.OwnerUserId}", ct).ConfigureAwait(false);
            rows.Add(new SeasonRewardPaidRow(winner.Place, winner.ChatId, winner.OwnerUserId, $"{winner.ClanTag} {winner.ClanName}", amount));
        }
        var result = new SeasonRewardProcessResult(rows.Count, rows);
        await AppendHistoryAsync(context, effect.SeasonId, "season.clan_reward_paid", new { effect.SeasonId, paid = rows.Count, winners = rows }, ct).ConfigureAwait(false);
        context.SetOutput("result", result);
    }
}
