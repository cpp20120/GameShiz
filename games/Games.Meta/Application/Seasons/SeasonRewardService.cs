using System.Globalization;
using Dapper;

namespace Games.Meta.Application.Seasons;

public sealed class SeasonRewardService(
    INpgsqlConnectionFactory connections,
    IEconomicsService economics) : ISeasonRewardService
{
    public async Task<SeasonRewardProcessResult> ProcessPlayerRewardsAsync(long seasonId, CancellationToken ct)
    {
        const string seasonSql = "SELECT config::text FROM meta_seasons WHERE id = @seasonId";
        const string topSql = """
            SELECT row_number() OVER (ORDER BY xp DESC, rating DESC, user_id ASC)::int AS Place,
                   chat_id AS ChatId,
                   user_id AS UserId,
                   display_name AS DisplayName,
                   xp AS Xp,
                   rating AS Rating
            FROM meta_season_players
            WHERE season_id = @seasonId
            ORDER BY xp DESC, rating DESC, user_id ASC
            LIMIT 10
            """;

        await using var conn = await connections.OpenAsync(ct);
        var configJson = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            seasonSql,
            new { seasonId },
            cancellationToken: ct));
        if (configJson is null)
            return new SeasonRewardProcessResult(0, []);

        var rewards = SeasonRewardsConfig.FromJson(configJson);
        var winners = (await conn.QueryAsync<PlayerSeasonRewardWinner>(new CommandDefinition(
            topSql,
            new { seasonId },
            cancellationToken: ct))).ToList();

        var paid = 0;
        var rows = new List<SeasonRewardPaidRow>();
        foreach (var winner in winners)
        {
            var amount = rewards.PlayerRewardForPlace(winner.Place);
            if (amount <= 0) continue;

            await economics.EnsureUserAsync(winner.UserId, winner.ChatId, winner.DisplayName, ct);
            await economics.CreditOnceAsync(
                winner.UserId,
                winner.ChatId,
                amount,
                "season.reward",
                string.Create(CultureInfo.InvariantCulture, $"season:reward:{seasonId}:{winner.Place}:{winner.ChatId}:{winner.UserId}"),
                ct);
            paid++;
            rows.Add(new SeasonRewardPaidRow(winner.Place, winner.ChatId, winner.UserId, winner.DisplayName, amount));
        }

        return new SeasonRewardProcessResult(paid, rows);
    }

    public async Task<SeasonRewardProcessResult> ProcessClanRewardsAsync(long seasonId, CancellationToken ct)
    {
        const string seasonSql = "SELECT config::text FROM meta_seasons WHERE id = @seasonId";
        const string topSql = """
            SELECT row_number() OVER (ORDER BY sc.xp DESC, sc.rating DESC, sc.clan_id ASC)::int AS Place,
                   sc.chat_id AS ChatId,
                   sc.clan_id AS ClanId,
                   c.name AS ClanName,
                   c.tag AS ClanTag,
                   c.owner_user_id AS OwnerUserId,
                   COALESCE(m.display_name, c.owner_user_id::text) AS OwnerDisplayName,
                   sc.xp AS Xp,
                   sc.rating AS Rating
            FROM meta_season_clans sc
            JOIN meta_clans c ON c.id = sc.clan_id
            LEFT JOIN meta_clan_members m ON m.clan_id = c.id AND m.user_id = c.owner_user_id
            WHERE sc.season_id = @seasonId
            ORDER BY sc.xp DESC, sc.rating DESC, sc.clan_id ASC
            LIMIT 10
            """;

        await using var conn = await connections.OpenAsync(ct);
        var configJson = await conn.ExecuteScalarAsync<string?>(new CommandDefinition(
            seasonSql,
            new { seasonId },
            cancellationToken: ct));
        if (configJson is null)
            return new SeasonRewardProcessResult(0, []);

        var rewards = SeasonRewardsConfig.FromJson(configJson);
        var winners = (await conn.QueryAsync<ClanSeasonRewardWinner>(new CommandDefinition(
            topSql,
            new { seasonId },
            cancellationToken: ct))).ToList();

        var paid = 0;
        var rows = new List<SeasonRewardPaidRow>();
        foreach (var winner in winners)
        {
            var amount = rewards.ClanRewardForPlace(winner.Place);
            if (amount <= 0) continue;

            await economics.EnsureUserAsync(winner.OwnerUserId, winner.ChatId, winner.OwnerDisplayName, ct);
            await economics.CreditOnceAsync(
                winner.OwnerUserId,
                winner.ChatId,
                amount,
                "season.clan_reward",
                $"season:clan-reward:{seasonId}:{winner.Place}:{winner.ChatId}:{winner.ClanId}:{winner.OwnerUserId}",
                ct);
            paid++;
            rows.Add(new SeasonRewardPaidRow(
                winner.Place,
                winner.ChatId,
                winner.OwnerUserId,
                $"{winner.ClanTag} {winner.ClanName}",
                amount));
        }

        return new SeasonRewardProcessResult(paid, rows);
    }

}
