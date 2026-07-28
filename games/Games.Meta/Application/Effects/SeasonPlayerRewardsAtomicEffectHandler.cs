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

internal sealed class SeasonPlayerRewardsAtomicEffectHandler : SeasonRewardsAtomicEffectHandler<SeasonPlayerRewardsAtomicEffect>
{
    protected override async Task ApplyAsync(SeasonPlayerRewardsAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct)
    {
        var configJson = await ConfigAsync(context, effect.SeasonId, ct);
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
            new { effect.SeasonId }, ct);
        var rows = new List<SeasonRewardPaidRow>();
        foreach (var winner in winners)
        {
            var amount = rewards.PlayerRewardForPlace(winner.Place);
            if (amount <= 0) continue;
            await CreditAsync(context, winner.UserId, winner.ChatId, winner.DisplayName, amount, "season.reward", $"season:reward:{effect.SeasonId}:{winner.Place}:{winner.ChatId}:{winner.UserId}", ct);
            rows.Add(new SeasonRewardPaidRow(winner.Place, winner.ChatId, winner.UserId, winner.DisplayName, amount));
        }
        var result = new SeasonRewardProcessResult(rows.Count, rows);
        await AppendHistoryAsync(context, effect.SeasonId, "season.reward_paid", new { effect.SeasonId, paid = rows.Count, winners = rows }, ct);
        context.SetOutput("result", result);
    }
}
