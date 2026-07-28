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

internal sealed class MetaSeasonClanRewardsAdminEffectHandler : MetaAdminEffectHandler<MetaSeasonClanRewardsAdminEffect>
{
    protected override async Task ApplyAsync(MetaSeasonClanRewardsAdminEffect effect, IAdminExecutionContext context, CancellationToken ct)
    {
        var configJson = await context.QuerySingleOrDefaultAsync<string?>("SELECT config::text FROM meta_seasons WHERE id = @seasonId", new { effect.SeasonId }, ct);
        if (configJson is null)
        {
            context.SetOutput("rows", Array.Empty<SeasonRewardPaidRow>());
            return;
        }

        var rewards = SeasonRewardsConfig.FromJson(configJson);
        var winners = await context.QueryAsync<ClanSeasonRewardWinner>(
            """
            SELECT row_number() OVER (ORDER BY sc.xp DESC, sc.rating DESC, sc.clan_id ASC)::int AS Place,
                   sc.chat_id AS ChatId, sc.clan_id AS ClanId, c.name AS ClanName, c.tag AS ClanTag,
                   c.owner_user_id AS OwnerUserId, COALESCE(m.display_name, c.owner_user_id::text) AS OwnerDisplayName
            FROM meta_season_clans sc
            JOIN meta_clans c ON c.id = sc.clan_id
            LEFT JOIN meta_clan_members m ON m.clan_id = c.id AND m.user_id = c.owner_user_id
            WHERE sc.season_id = @seasonId
            ORDER BY sc.xp DESC, sc.rating DESC, sc.clan_id ASC
            LIMIT 10
            """,
            new { effect.SeasonId }, ct);
        var rows = new List<SeasonRewardPaidRow>();
        foreach (var winner in winners)
        {
            var amount = rewards.ClanRewardForPlace(winner.Place);
            if (amount <= 0) continue;
            await CreditAsync(context, winner.OwnerUserId, winner.ChatId, winner.OwnerDisplayName, amount, "season.clan_reward",
                $"season:clan-reward:{effect.SeasonId}:{winner.Place}:{winner.ChatId}:{winner.ClanId}:{winner.OwnerUserId}", ct);
            rows.Add(new SeasonRewardPaidRow(winner.Place, winner.ChatId, winner.OwnerUserId, $"{winner.ClanTag} {winner.ClanName}", amount));
        }

        context.SetOutput("rows", rows);
        await AppendHistoryAsync(context, "season.clan_reward_paid", "season", effect.SeasonId.ToString(CultureInfo.InvariantCulture), effect.SeasonId,
            new { effect.SeasonId, paid = rows.Count, winners = rows }, ct);
    }
}
