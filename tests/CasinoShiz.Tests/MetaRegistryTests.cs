using BotFramework.Sdk;
using Games.Meta;
using System.Text.Json;
using System.Text.Json.Nodes;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class MetaRegistryTests
{
    [Fact]
    public void MetaMigrations_HaveUniqueIds()
    {
        var ids = new MetaMigrations().Migrations.Select(x => x.Id).ToArray();

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("008_meta_event_log", ids);
        Assert.Contains("009_game_streaks", ids);
    }

    [Fact]
    public void SeasonPlanFactory_CreatesThirtyBiweeklyPlansWithRotatingMeta()
    {
        var startsAt = new DateTimeOffset(2026, 6, 22, 0, 0, 0, TimeSpan.Zero);

        var plans = SeasonPlanFactory.CreatePlans(startsAt);

        Assert.Equal(30, plans.Count);
        Assert.All(plans, plan => Assert.Equal(14, (plan.EndsAt - plan.StartsAt).TotalDays));
        for (var i = 1; i < plans.Count; i++)
            Assert.Equal(plans[i - 1].EndsAt, plans[i].StartsAt);

        var themes = plans
            .Select(plan => JsonDocument.Parse(plan.ConfigJson).RootElement
                .GetProperty("season")
                .GetProperty("theme")
                .GetString())
            .ToHashSet(StringComparer.Ordinal);

        Assert.True(themes.Count > 1);
    }

    [Fact]
    public void SeasonProgressionConfig_FromJson_DrivesXpRatingAndLevelCurve()
    {
        const string json = """
            {
              "xp": {
                "play": 10,
                "win": 50,
                "loss": 5,
                "stakeMultiplier": 0.1,
                "maxXpPerGame": 200
              },
              "rating": {
                "enabled": true,
                "start": 1200,
                "winDelta": 20,
                "lossDelta": -15
              },
              "levels": {
                "xpPerLevelSquaredBase": 25
              }
            }
            """;

        var config = SeasonProgressionConfig.FromJson(json);

        Assert.Equal(160, config.CalculateXpDelta(1_000, isWin: true));
        Assert.Equal(115, config.CalculateXpDelta(1_000, isWin: false));
        Assert.Equal(20, config.CalculateRatingDelta(isWin: true));
        Assert.Equal(-15, config.CalculateRatingDelta(isWin: false));
        Assert.Equal(1_200, config.RatingStart);
        Assert.Equal(3, config.LevelForXp(100));
        Assert.Equal(225, config.XpForLevel(4));
    }

    [Fact]
    public void SeasonProgressionConfig_FromJson_InvalidJsonUsesDefaults()
    {
        var config = SeasonProgressionConfig.FromJson("{ nope");

        Assert.Equal(40, config.CalculateXpDelta(1_000, isWin: true));
        Assert.Equal(17, config.CalculateXpDelta(1_000, isWin: false));
        Assert.Equal(16, config.CalculateRatingDelta(isWin: true));
        Assert.Equal(-12, config.CalculateRatingDelta(isWin: false));
        Assert.Equal(2, config.LevelForXp(100));
    }

    [Fact]
    public void SeasonRewardsConfig_FromJson_UsesConfiguredTopRewards()
    {
        const string json = """
            {
              "rewards": {
                "playerTop": [9000, 4500, 1000, 0, -50],
                "clanTop": [18000, 9000, 3000]
              }
            }
            """;

        var config = SeasonRewardsConfig.FromJson(json);

        Assert.Equal(9_000, config.PlayerRewardForPlace(1));
        Assert.Equal(4_500, config.PlayerRewardForPlace(2));
        Assert.Equal(1_000, config.PlayerRewardForPlace(3));
        Assert.Equal(0, config.PlayerRewardForPlace(4));
        Assert.Equal(18_000, config.ClanRewardForPlace(1));
        Assert.Equal(3_000, config.ClanRewardForPlace(3));
        Assert.Equal(0, config.ClanRewardForPlace(4));
    }

    [Fact]
    public void SeasonQuestRotationConfig_FromJson_NormalizesFocusAndRarityBias()
    {
        const string json = """
            {
              "quests": {
                "focus": "High_Stake",
                "rarityBias": "Rare"
              }
            }
            """;

        var config = SeasonQuestRotationConfig.FromJson(json);

        Assert.Equal("high-stake", config.Focus);
        Assert.Equal("rare", config.RarityBias);
    }

    [Fact]
    public void AchievementRegistry_AllIdsAreUnique()
    {
        var ids = AchievementRegistry.All.Select(x => x.Id).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void GameStreakRegistry_CreatesThreeAchievementsPerSupportedGame()
    {
        var achievements = GameStreakRegistry.GetAchievements();

        Assert.Equal(GameStreakRegistry.Games.Count * 3, achievements.Count);
        Assert.Equal(achievements.Count, achievements.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData(2, 0)]
    [InlineData(3, 1)]
    [InlineData(7, 2)]
    [InlineData(14, 3)]
    [InlineData(30, 3)]
    public void GameStreakRegistry_Evaluate_UnlocksReachedMilestones(int currentStreak, int expectedCount)
    {
        var streak = new GameStreak(
            1, 100, 42, MiniGameIds.Darts, currentStreak, currentStreak, currentStreak,
            new DateOnly(2026, 6, 13), DateTimeOffset.UtcNow);

        var unlocked = GameStreakRegistry.Evaluate(streak);

        Assert.Equal(expectedCount, unlocked.Count);
        Assert.All(unlocked, x => Assert.StartsWith("streak_darts_", x.Id));
    }

    [Fact]
    public void GameStreakRegistry_PlayDay_UsesConfiguredTimezone()
    {
        var occurredAt = new DateTimeOffset(2026, 6, 12, 20, 30, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        var playDay = GameStreakRegistry.PlayDay(occurredAt, 7);

        Assert.Equal(new DateOnly(2026, 6, 13), playDay);
    }

    [Theory]
    [InlineData(2026, 6, 13, 5)]
    [InlineData(2026, 6, 12, 5)]
    [InlineData(2026, 6, 11, 0)]
    public void GameStreakRegistry_ActiveStreak_ExpiresAfterMissedDay(
        int year,
        int month,
        int day,
        int expected)
    {
        var today = new DateOnly(2026, 6, 13);

        var active = GameStreakRegistry.ActiveStreak(5, new DateOnly(year, month, day), today);

        Assert.Equal(expected, active);
    }

    [Fact]
    public void QuestRegistry_AllIdsAreUnique()
    {
        var ids = QuestRegistry.All.Select(x => x.Id).ToArray();

        Assert.True(ids.Length > 3_000);
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void QuestRegistry_ActiveFor_IsStableWithinPeriodAndRotates()
    {
        var season = new MetaSeason(1, "S1", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(1), "active", "{}");
        var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);

        var first = QuestRegistry.ActiveFor(season, 100, 42, now).Select(x => x.Id).ToArray();
        var second = QuestRegistry.ActiveFor(season, 100, 42, now.AddHours(3)).Select(x => x.Id).ToArray();
        var tomorrow = QuestRegistry.ActiveFor(season, 100, 42, now.AddDays(1)).Select(x => x.Id).ToArray();

        Assert.Equal(7, first.Length);
        Assert.Equal(first.Length, first.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(first, second);
        Assert.NotEqual(first, tomorrow);
    }

    [Fact]
    public void QuestRegistry_ActiveFor_NewPlayerDoesNotReceiveProgressLockedQuests()
    {
        var season = new MetaSeason(1, "S1", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(1), "active", "{}");
        var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var progress = new QuestPlayerProgress(0, 0, 0);

        var quests = Enumerable.Range(0, 30)
            .SelectMany(day => QuestRegistry.ActiveFor(season, 100, 42, now.AddDays(day), progress))
            .ToArray();

        Assert.NotEmpty(quests);
        Assert.All(quests, q => Assert.Equal(0, q.MinLevel));
        Assert.All(quests, q => Assert.Equal(0, q.MinGamesPlayed));
        Assert.All(quests, q => Assert.Equal(0, q.MinTotalStaked));
        Assert.DoesNotContain(quests, q => q.Rarity is "rare" or "epic" or "legendary");
    }

    [Fact]
    public void QuestRegistry_ActiveFor_VeteranCanReceiveProgressLockedQuests()
    {
        var season = new MetaSeason(1, "S1", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(1), "active", "{}");
        var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var progress = new QuestPlayerProgress(10, 100, 100_000);

        var quests = Enumerable.Range(0, 60)
            .SelectMany(day => QuestRegistry.ActiveFor(season, 100, 42, now.AddDays(day), progress))
            .ToArray();

        Assert.Contains(quests, q => q.MinLevel > 0 || q.MinGamesPlayed > 0 || q.MinTotalStaked > 0);
        Assert.Contains(quests, q => q.Rarity is "rare" or "epic");
    }

    [Fact]
    public void AchievementRegistry_Evaluate_FirstWinLargePayout_ReturnsExpectedAchievements()
    {
        var ev = new GameCompletedMetaEvent(
            ChatId: 100,
            UserId: 42,
            DisplayName: "u",
            GameKey: MiniGameIds.Dice,
            Stake: 100,
            Payout: 1_000,
            IsWin: true,
            Multiplier: 10,
            OccurredAt: 1);
        var player = new SeasonPlayer(
            SeasonId: 1,
            ChatId: 100,
            UserId: 42,
            DisplayName: "u",
            Xp: 100,
            Level: 2,
            Rating: 1016,
            GamesPlayed: 1,
            Wins: 1,
            Losses: 0,
            TotalStaked: 100,
            TotalPayout: 1_000,
            UpdatedAt: DateTimeOffset.UtcNow);

        var ids = AchievementRegistry.Evaluate(ev, player).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("first_game", ids);
        Assert.Contains("first_win", ids);
        Assert.Contains("big_payout", ids);
        Assert.Contains("dice_player", ids);
    }

    [Fact]
    public void AchievementRegistry_Evaluate_SeasonThresholds_ReturnsExpectedAchievements()
    {
        var ev = new GameCompletedMetaEvent(100, 42, "u", MiniGameIds.Darts, 50, 0, false, 0, 1);
        var player = new SeasonPlayer(
            SeasonId: 1,
            ChatId: 100,
            UserId: 42,
            DisplayName: "u",
            Xp: 500,
            Level: 3,
            Rating: 900,
            GamesPlayed: 50,
            Wins: 10,
            Losses: 40,
            TotalStaked: 10_000,
            TotalPayout: 2_000,
            UpdatedAt: DateTimeOffset.UtcNow);

        var ids = AchievementRegistry.Evaluate(ev, player).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("ten_games", ids);
        Assert.Contains("fifty_games", ids);
        Assert.Contains("ten_wins", ids);
        Assert.Contains("high_roller", ids);
        Assert.Contains("darts_player", ids);
    }

    [Theory]
    [InlineData(999, 1_000, false)]
    [InlineData(1_000, 1_000, true)]
    [InlineData(4_999, 5_000, false)]
    [InlineData(5_000, 5_000, true)]
    public void AchievementRegistry_Evaluate_HighRollerUsesConfiguredThreshold(
        long totalStaked,
        long threshold,
        bool expected)
    {
        var ev = new GameCompletedMetaEvent(100, 42, "u", MiniGameIds.Dice, 10, 0, false, 0, 1);
        var player = new SeasonPlayer(
            SeasonId: 1,
            ChatId: 100,
            UserId: 42,
            DisplayName: "u",
            Xp: 10,
            Level: 1,
            Rating: 1000,
            GamesPlayed: 1,
            Wins: 0,
            Losses: 1,
            TotalStaked: totalStaked,
            TotalPayout: 0,
            UpdatedAt: DateTimeOffset.UtcNow);

        var unlocked = AchievementRegistry.Evaluate(ev, player, threshold);

        Assert.Equal(expected, unlocked.Any(x => x.Id == "high_roller"));
    }

    [Fact]
    public void AchievementRegistry_GetAll_HighRollerDescriptionUsesConfiguredThreshold()
    {
        var achievement = AchievementRegistry.GetAll(1_000).Single(x => x.Id == "high_roller");

        Assert.Contains("1 000", achievement.Description);
    }

    [Theory]
    [InlineData(999, 1_000, false)]
    [InlineData(1_000, 1_000, true)]
    [InlineData(4_999, 5_000, false)]
    [InlineData(5_000, 5_000, true)]
    public void AchievementRegistry_Evaluate_BigPayoutUsesConfiguredThreshold(
        long payout,
        long threshold,
        bool expected)
    {
        var ev = new GameCompletedMetaEvent(100, 42, "u", MiniGameIds.Dice, 10, payout, true, 1, 1);
        var player = new SeasonPlayer(
            SeasonId: 1,
            ChatId: 100,
            UserId: 42,
            DisplayName: "u",
            Xp: 10,
            Level: 1,
            Rating: 1000,
            GamesPlayed: 1,
            Wins: 1,
            Losses: 0,
            TotalStaked: 10,
            TotalPayout: payout,
            UpdatedAt: DateTimeOffset.UtcNow);

        var unlocked = AchievementRegistry.Evaluate(ev, player, 10_000, threshold);

        Assert.Equal(expected, unlocked.Any(x => x.Id == "big_payout"));
    }

    [Fact]
    public void AchievementRegistry_GetAll_BigPayoutDescriptionUsesConfiguredThreshold()
    {
        var achievement = AchievementRegistry.GetAll(1_000, 5_000).Single(x => x.Id == "big_payout");

        Assert.Contains("5 000", achievement.Description);
    }

    [Fact]
    public void RuntimeTuningSanitizer_AllowsMetaSettings()
    {
        var raw = new JsonObject
        {
            ["Games"] = new JsonObject
            {
                ["meta"] = new JsonObject
                {
                    ["HighRollerTotalStaked"] = 1_000,
                    ["BigPayoutMinimum"] = 5_000,
                },
            },
        };

        var sanitized = RuntimeTuningPayloadSanitizer.Sanitize(raw);

        Assert.Equal(1_000, sanitized["Games"]?["meta"]?["HighRollerTotalStaked"]?.GetValue<int>());
        Assert.Equal(5_000, sanitized["Games"]?["meta"]?["BigPayoutMinimum"]?.GetValue<int>());
    }

    [Fact]
    public void QuestRegistry_Matching_LosingDiceGame_MatchesOnlyActivePlayableQuests()
    {
        var season = new MetaSeason(1, "S1", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddYears(1), "active", "{}");
        var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var ev = new GameCompletedMetaEvent(100, 42, "u", MiniGameIds.Dice, 250, 0, false, 0, now.ToUnixTimeMilliseconds());
        var activeIds = QuestRegistry.ActiveFor(season, ev.ChatId, ev.UserId, now).Select(x => x.Id).ToHashSet(StringComparer.Ordinal);
        var matching = QuestRegistry.Matching(season, ev.ChatId, ev.UserId, ev).ToArray();

        Assert.NotEmpty(matching);
        Assert.All(matching, q => Assert.Contains(q.Id, activeIds.AsEnumerable()));
        Assert.All(matching, q => Assert.True(q.Kind is "play" or "volume" or "loss" or "low_stake" or "high_stake"));
        Assert.All(matching.Where(q => q.GameKey is not null), q => Assert.Equal(MiniGameIds.Dice, q.GameKey));
    }

    [Fact]
    public void QuestRegistry_DeltaFor_UsesKindSpecificProgress()
    {
        var ev = new GameCompletedMetaEvent(100, 42, "u", MiniGameIds.Dice, 250, 600, true, 0, 1);
        var volume = QuestRegistry.All.First(x => x.Kind == "volume");
        var payout = QuestRegistry.All.First(x => x.Kind == "payout");
        var profit = QuestRegistry.All.First(x => x.Kind == "profit");
        var loss = QuestRegistry.All.First(x => x.Kind == "loss");
        var multiplier = QuestRegistry.All.First(x => x.Kind == "multiplier");
        var play = QuestRegistry.All.First(x => x.Kind == "play");

        Assert.Equal(250, QuestRegistry.DeltaFor(volume, ev));
        Assert.Equal(600, QuestRegistry.DeltaFor(payout, ev));
        Assert.Equal(350, QuestRegistry.DeltaFor(profit, ev));
        Assert.Equal(1, QuestRegistry.DeltaFor(loss, ev));
        Assert.Equal(1, QuestRegistry.DeltaFor(multiplier, ev));
        Assert.Equal(1, QuestRegistry.DeltaFor(play, ev));
    }

    [Fact]
    public void QuestRegistry_PeriodKey_DailyAndWeeklyAreStable()
    {
        var now = new DateTimeOffset(2026, 5, 20, 12, 0, 0, TimeSpan.Zero);
        var daily = QuestRegistry.All.First(x => x.Period == "daily");
        var weekly = QuestRegistry.All.First(x => x.Period == "weekly");

        Assert.Equal("2026-05-20", QuestRegistry.PeriodKey(daily, now));
        Assert.Equal("2026-W21", QuestRegistry.PeriodKey(weekly, now));
    }
}
