using System.Globalization;
using System.Text.Json;
using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Admin.Execution;
using Dapper;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Models;
using Games.Meta.Infrastructure.Catalog;
using Games.Meta.Domain.Seasons;

namespace Games.Meta.Application.Effects;

internal sealed class MetaSeasonPlayerRewardsAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonPlayerRewardsAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonPlayerRewardsAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var configJson = await context.QuerySingleOrDefaultAsync<string?>("SELECT config::text FROM meta_seasons WHERE id = @seasonId", new { effect.SeasonId }, ct);
        if (configJson is null)
        {
            context.SetOutput("rows", Array.Empty<SeasonRewardPaidRow>());
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
            await CreditAsync(context, winner.UserId, winner.ChatId, winner.DisplayName, amount, "season.reward",
                $"season:reward:{effect.SeasonId}:{winner.Place}:{winner.ChatId}:{winner.UserId}", ct);
            rows.Add(new SeasonRewardPaidRow(winner.Place, winner.ChatId, winner.UserId, winner.DisplayName, amount));
        }

        context.SetOutput("rows", rows);
        await AppendHistoryAsync(context, "season.reward_paid", "season", effect.SeasonId.ToString(CultureInfo.InvariantCulture), effect.SeasonId,
            new { effect.SeasonId, paid = rows.Count, winners = rows }, ct);
    }
}
