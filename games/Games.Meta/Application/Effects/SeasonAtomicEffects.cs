using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Dapper;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Models;
using Games.Meta.Domain.Seasons;
using Microsoft.Extensions.Options;

namespace Games.Meta.Application.Effects;

public sealed record SeasonPlayerRewardsAtomicEffect(long SeasonId) : IAtomicEffect;
public sealed record SeasonClanRewardsAtomicEffect(long SeasonId) : IAtomicEffect;

internal abstract class SeasonRewardsAtomicEffectHandler<TEffect>(
    IOptions<BotFrameworkOptions> options) : AtomicEffectHandler<TEffect>
    where TEffect : class, IAtomicEffect
{
    protected readonly int StartingCoins = options.Value.StartingCoins;

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
        await context.ExecuteAsync(
            """
            INSERT INTO users (telegram_user_id, balance_scope_id, display_name, coins)
            VALUES (@userId, @chatId, @displayName, @startingCoins)
            ON CONFLICT (telegram_user_id, balance_scope_id)
            DO UPDATE SET display_name = EXCLUDED.display_name, updated_at = now()
            """,
            new { userId, chatId, displayName, startingCoins = StartingCoins }, ct).ConfigureAwait(false);
        var existing = await context.QuerySingleOrDefaultAsync<int?>("SELECT balance_after FROM economics_ledger WHERE operation_id = @operationId", new { operationId }, ct).ConfigureAwait(false);
        if (existing.HasValue) return;
        var wallet = await context.QuerySingleOrDefaultAsync<WalletRow>(
            "SELECT coins AS Coins, version AS Version FROM users WHERE telegram_user_id = @userId AND balance_scope_id = @chatId FOR UPDATE",
            new { userId, chatId }, ct).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Season reward wallet is missing.");
        existing = await context.QuerySingleOrDefaultAsync<int?>("SELECT balance_after FROM economics_ledger WHERE operation_id = @operationId", new { operationId }, ct).ConfigureAwait(false);
        if (existing.HasValue) return;
        var balance = checked(wallet.Coins + amount);
        await context.ExecuteAsync("UPDATE users SET coins = @balance, version = @version, updated_at = now() WHERE telegram_user_id = @userId AND balance_scope_id = @chatId", new { userId, chatId, balance, version = checked(wallet.Version + 1) }, ct).ConfigureAwait(false);
        await context.ExecuteAsync("INSERT INTO economics_ledger (telegram_user_id, balance_scope_id, delta, balance_after, reason, operation_id) VALUES (@userId, @chatId, @amount, @balance, @reason, @operationId)", new { userId, chatId, amount, balance, reason, operationId }, ct).ConfigureAwait(false);
    }

    protected static Task AppendHistoryAsync(IAtomicEffectContext context, long seasonId, string eventType, object payload, CancellationToken ct) =>
        context.ExecuteAsync(
            "INSERT INTO meta_event_log (event_type, aggregate_type, aggregate_id, season_id, payload) VALUES (@eventType, 'season', @aggregateId, @seasonId, CAST(@payload AS jsonb))",
            new { eventType, aggregateId = seasonId.ToString(CultureInfo.InvariantCulture), seasonId, payload = JsonSerializer.Serialize(payload) }, ct);

    protected sealed record WalletRow(int Coins, long Version);
}

internal sealed class SeasonPlayerRewardsAtomicEffectHandler(
    IOptions<BotFrameworkOptions> options) : SeasonRewardsAtomicEffectHandler<SeasonPlayerRewardsAtomicEffect>(options)
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

internal sealed class SeasonClanRewardsAtomicEffectHandler(
    IOptions<BotFrameworkOptions> options) : SeasonRewardsAtomicEffectHandler<SeasonClanRewardsAtomicEffect>(options)
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
