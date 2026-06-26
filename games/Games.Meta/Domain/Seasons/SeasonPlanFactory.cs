using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Games.Meta.Domain.Seasons;

public static class SeasonPlanFactory
{
    public const int DefaultDurationDays = 14;
    public const int DefaultPreparedSeasonCount = 30;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static readonly SeasonTheme[] Themes =
    [
        new("balanced", "Shizoid League", 5, 25, 2, 0.010m, 500, 16, -12, 20, 3, 20, 12, "all-round", "normal"),
        new("quest_rush", "Quest Rush", 7, 24, 3, 0.010m, 560, 14, -10, 20, 3, 18, 10, "daily", "uncommon"),
        new("high_roller", "High Roller", 4, 28, 2, 0.014m, 650, 18, -14, 20, 2, 24, 12, "volume", "rare"),
        new("clan_wars", "Clan Wars", 5, 23, 2, 0.009m, 520, 15, -11, 24, 3, 20, 10, "clans", "normal"),
        new("tournament_arc", "Tournament Arc", 5, 26, 2, 0.010m, 540, 17, -12, 20, 5, 20, 12, "tournaments", "uncommon"),
        new("jackpot_hunt", "Jackpot Hunt", 4, 30, 2, 0.012m, 620, 18, -13, 20, 3, 18, 9, "payout", "rare"),
        new("streak_sprint", "Streak Sprint", 6, 24, 3, 0.009m, 540, 15, -10, 20, 3, 16, 8, "streaks", "normal"),
        new("risk_watch", "Risk Watch", 5, 25, 2, 0.010m, 500, 16, -12, 20, 3, 14, 7, "controlled", "normal"),
    ];

    public static string DefaultConfigJson => BuildConfigJson(1);

    public static IReadOnlyList<PreparedSeasonPlan> CreatePlans(
        DateTimeOffset firstStartsAt,
        int count = DefaultPreparedSeasonCount,
        int durationDays = DefaultDurationDays,
        int startNumber = 1)
    {
        count = Math.Clamp(count, 1, 100);
        durationDays = Math.Clamp(durationDays, 1, 365);
        startNumber = Math.Max(1, startNumber);

        var plans = new List<PreparedSeasonPlan>(count);
        var startsAt = firstStartsAt;
        for (var i = 0; i < count; i++)
        {
            var seasonNumber = startNumber + i;
            var endsAt = startsAt.AddDays(durationDays);
            plans.Add(new PreparedSeasonPlan(
                NameFor(seasonNumber),
                startsAt,
                endsAt,
                BuildConfigJson(seasonNumber, durationDays)));
            startsAt = endsAt;
        }

        return plans;
    }

    public static string NameFor(int seasonNumber)
    {
        var theme = ThemeFor(seasonNumber);
        return string.Create(CultureInfo.InvariantCulture, $"Season {seasonNumber:00}: {theme.Title}");
    }

    public static string BuildConfigJson(int seasonNumber, int durationDays = DefaultDurationDays)
    {
        var theme = ThemeFor(seasonNumber);
        var root = new JsonObject
        {
            ["season"] = new JsonObject
            {
                ["number"] = Math.Max(1, seasonNumber),
                ["theme"] = theme.Key,
                ["durationDays"] = Math.Clamp(durationDays, 1, 365),
            },
            ["xp"] = new JsonObject
            {
                ["play"] = theme.PlayXp,
                ["win"] = theme.WinXp,
                ["loss"] = theme.LossXp,
                ["stakeMultiplier"] = theme.StakeMultiplier,
                ["maxXpPerGame"] = theme.MaxXpPerGame,
            },
            ["rating"] = new JsonObject
            {
                ["enabled"] = true,
                ["start"] = 1_000,
                ["winDelta"] = theme.WinRatingDelta,
                ["lossDelta"] = theme.LossRatingDelta,
            },
            ["levels"] = new JsonObject
            {
                ["xpPerLevelSquaredBase"] = 100,
            },
            ["rewards"] = new JsonObject
            {
                ["playerTop"] = new JsonArray(5_000, 2_500, 1_000),
                ["clanTop"] = new JsonArray(10_000, 5_000, 2_500),
            },
            ["quests"] = new JsonObject
            {
                ["dailyEnabled"] = true,
                ["weeklyEnabled"] = true,
                ["focus"] = theme.QuestFocus,
                ["rarityBias"] = theme.RarityBias,
            },
            ["achievements"] = new JsonObject
            {
                ["enabled"] = true,
            },
            ["clans"] = new JsonObject
            {
                ["enabled"] = true,
                ["maxMembers"] = theme.MaxClanMembers,
            },
            ["tournaments"] = new JsonObject
            {
                ["enabled"] = true,
                ["maxActivePerChat"] = theme.MaxActiveTournamentsPerChat,
            },
            ["risk"] = new JsonObject
            {
                ["enabled"] = true,
                ["largeWinMultiplierAlert"] = theme.LargeWinMultiplierAlert,
                ["suspiciousStreakThreshold"] = theme.SuspiciousStreakThreshold,
            },
        };

        return root.ToJsonString(JsonOptions);
    }

    private static SeasonTheme ThemeFor(int seasonNumber) =>
        Themes[(Math.Max(1, seasonNumber) - 1) % Themes.Length];

}
