using System.Text.Json;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class SeasonConfigTests
{
    [Fact]
    public void SeasonProgressionConfig_FromSeason_ParsesStringValuesAndClampsBounds()
    {
        const string json = """
            {
              "xp": {
                "play": "-10",
                "win": "20000",
                "loss": "7",
                "stakeMultiplier": "12.5",
                "maxXpPerGame": 0,
                "minXpPerGame": 250
              },
              "rating": {
                "enabled": "false",
                "start": "-1",
                "winDelta": "1000001",
                "lossDelta": "-1000001"
              },
              "progression": {
                "xpPerLevelSquaredBase": "0"
              }
            }
            """;
        var season = new MetaSeason(7, "clamped", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "active", json);

        var config = SeasonProgressionConfig.FromSeason(season);

        Assert.Equal(0, config.PlayXp);
        Assert.Equal(10_000, config.WinXp);
        Assert.Equal(7, config.LossXp);
        Assert.Equal(10m, config.StakeMultiplier);
        Assert.Equal(1, config.MaxXpPerGame);
        Assert.Equal(250, config.MinXpPerGame);
        Assert.False(config.RatingEnabled);
        Assert.Equal(0, config.RatingStart);
        Assert.Equal(1_000_000, config.RatingWinDelta);
        Assert.Equal(-1_000_000, config.RatingLossDelta);
        Assert.Equal(1, config.XpPerLevelSquaredBase);
        Assert.Equal(0, config.CalculateRatingDelta(isWin: true));
        Assert.Equal(250, config.CalculateXpDelta(1, isWin: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[]")]
    [InlineData("{\"xp\":42}")]
    public void SeasonProgressionConfig_FromJson_FallsBackForMissingOrWrongShape(string? json)
    {
        var config = SeasonProgressionConfig.FromJson(json);

        Assert.Equal(40, config.CalculateXpDelta(1_000, isWin: true));
        Assert.Equal(16, config.CalculateRatingDelta(isWin: true));
        Assert.Equal(2, config.LevelForXp(100));
    }

    [Theory]
    [InlineData(-100, true, 1)]
    [InlineData(0, false, 1)]
    [InlineData(50_000, true, 500)]
    public void SeasonProgressionConfig_CalculateXpDelta_ClampsStakeAndResult(long stake, bool isWin, long expected)
    {
        var config = new SeasonProgressionConfig
        {
            PlayXp = 0,
            WinXp = 0,
            LossXp = 0,
            StakeMultiplier = 0.1m,
            MinXpPerGame = 1,
            MaxXpPerGame = 500,
        };

        Assert.Equal(expected, config.CalculateXpDelta(stake, isWin));
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(99, 1)]
    [InlineData(100, 2)]
    [InlineData(399, 2)]
    [InlineData(400, 3)]
    public void SeasonProgressionConfig_LevelForXp_UsesSquaredCurve(long xp, int expectedLevel)
    {
        var config = new SeasonProgressionConfig { XpPerLevelSquaredBase = 100 };

        Assert.Equal(expectedLevel, config.LevelForXp(xp));
        Assert.Equal(0, config.XpForLevel(0));
        Assert.Equal(0, config.XpForLevel(1));
        Assert.Equal(100, config.XpForLevel(2));
    }

    [Fact]
    public void SeasonRewardsConfig_FromSeason_FiltersInvalidRewardsAndCapsListLength()
    {
        const string json = """
            {
              "rewards": {
                "playerTop": [10, -1, 0, 20, "bad", 30, 40, 50, 60, 70, 80, 90, 100],
                "clanTop": [100, 50]
              }
            }
            """;
        var season = new MetaSeason(1, "rewards", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, "active", json);

        var config = SeasonRewardsConfig.FromSeason(season);

        Assert.Equal(10, config.PlayerRewardForPlace(1));
        Assert.Equal(20, config.PlayerRewardForPlace(2));
        Assert.Equal(100, config.PlayerRewardForPlace(10));
        Assert.Equal(0, config.PlayerRewardForPlace(11));
        Assert.Equal(100, config.ClanRewardForPlace(1));
        Assert.Equal(50, config.ClanRewardForPlace(2));
        Assert.Equal(0, config.ClanRewardForPlace(0));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{ nope")]
    [InlineData("{\"rewards\":{\"playerTop\":[],\"clanTop\":[]}}")]
    [InlineData("{\"rewards\":[]}")]
    public void SeasonRewardsConfig_FromJson_UsesDefaultsWhenRewardsAreMissingInvalidOrEmpty(string? json)
    {
        var config = SeasonRewardsConfig.FromJson(json);

        Assert.Equal(5_000, config.PlayerRewardForPlace(1));
        Assert.Equal(2_500, config.PlayerRewardForPlace(2));
        Assert.Equal(10_000, config.ClanRewardForPlace(1));
        Assert.Equal(0, config.PlayerRewardForPlace(99));
    }

    [Theory]
    [InlineData(null, "all-round", "normal")]
    [InlineData("{ nope", "all-round", "normal")]
    [InlineData("{}", "all-round", "normal")]
    [InlineData("{\"quests\":{\"focus\":\"  \",\"rarityBias\":\"LEGENDARY_DROP\"}}", "normal", "legendary-drop")]
    public void SeasonQuestRotationConfig_FromJson_NormalizesOrFallsBack(
        string? json,
        string expectedFocus,
        string expectedRarityBias)
    {
        var config = SeasonQuestRotationConfig.FromJson(json);

        Assert.Equal(expectedFocus, config.Focus);
        Assert.Equal(expectedRarityBias, config.RarityBias);
    }

    [Fact]
    public void SeasonQuestRotationConfig_FromSeason_ReadsQuestConfig()
    {
        var season = new MetaSeason(
            1,
            "quests",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch,
            "active",
            "{\"quests\":{\"focus\":\"Clan_Wars\",\"rarityBias\":\"Uncommon\"}}");

        var config = SeasonQuestRotationConfig.FromSeason(season);

        Assert.Equal("clan-wars", config.Focus);
        Assert.Equal("uncommon", config.RarityBias);
    }

    [Fact]
    public void SeasonPlanFactory_CreatePlans_ClampsArgumentsAndChainsDates()
    {
        var startsAt = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);

        var plans = SeasonPlanFactory.CreatePlans(startsAt, count: 500, durationDays: 500, startNumber: -10);

        Assert.Equal(100, plans.Count);
        Assert.Equal("Season 01: Shizoid League", plans[0].Name);
        Assert.All(plans, plan => Assert.Equal(365, (plan.EndsAt - plan.StartsAt).TotalDays));
        for (var i = 1; i < plans.Count; i++)
            Assert.Equal(plans[i - 1].EndsAt, plans[i].StartsAt);
    }

    [Fact]
    public void SeasonPlanFactory_BuildConfigJson_ClampsSeasonAndDurationAndRotatesTheme()
    {
        var first = JsonDocument.Parse(SeasonPlanFactory.BuildConfigJson(-5, durationDays: 0)).RootElement;
        var ninth = JsonDocument.Parse(SeasonPlanFactory.BuildConfigJson(9, durationDays: 999)).RootElement;

        Assert.Equal(1, first.GetProperty("season").GetProperty("number").GetInt32());
        Assert.Equal("balanced", first.GetProperty("season").GetProperty("theme").GetString());
        Assert.Equal(1, first.GetProperty("season").GetProperty("durationDays").GetInt32());
        Assert.Equal("balanced", ninth.GetProperty("season").GetProperty("theme").GetString());
        Assert.Equal(365, ninth.GetProperty("season").GetProperty("durationDays").GetInt32());
        Assert.Equal("all-round", first.GetProperty("quests").GetProperty("focus").GetString());
        Assert.True(first.GetProperty("achievements").GetProperty("enabled").GetBoolean());
    }

    [Fact]
    public void SeasonPlanFactory_DefaultConfigJson_IsReadableBySeasonConfigs()
    {
        var progression = SeasonProgressionConfig.FromJson(SeasonPlanFactory.DefaultConfigJson);
        var rewards = SeasonRewardsConfig.FromJson(SeasonPlanFactory.DefaultConfigJson);
        var quests = SeasonQuestRotationConfig.FromJson(SeasonPlanFactory.DefaultConfigJson);

        Assert.Equal(40, progression.CalculateXpDelta(1_000, isWin: true));
        Assert.Equal(5_000, rewards.PlayerRewardForPlace(1));
        Assert.Equal("all-round", quests.Focus);
        Assert.Equal("normal", quests.RarityBias);
    }
}
