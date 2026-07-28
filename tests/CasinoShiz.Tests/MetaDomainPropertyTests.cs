using BotFramework.Sdk.Events.Meta;
using BotFramework.Sdk.MiniGames;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Games.Meta.Domain.Achievements;
using Games.Meta.Domain.Quests;
using Games.Meta.Domain.Seasons;
using Games.Meta.Domain.Streaks;
using Games.Meta.Infrastructure.Catalog;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class MetaDomainPropertyTests
{
    [Property(MaxTest = 100)]
    public Property SeasonProgression_XpIsBoundedAndMonotonic(
        NonNegativeInt rawStake,
        NonNegativeInt rawDelta)
    {
        var stake = (long)rawStake.Get;
        var nextStake = stake + rawDelta.Get;
        var config = new SeasonProgressionConfig();
        var first = config.CalculateXpDelta(stake, isWin: true);
        var second = config.CalculateXpDelta(nextStake, isWin: true);

        return (first >= config.MinXpPerGame
                && first <= config.MaxXpPerGame
                && second >= config.MinXpPerGame
                && second <= config.MaxXpPerGame
                && first <= second)
            .ToProperty()
            .Label($"stake={stake}, nextStake={nextStake}, first={first}, second={second}");
    }

    [Property(MaxTest = 100)]
    public Property SeasonProgression_LevelThresholdsRoundTrip(PositiveInt rawLevel)
    {
        var level = 1 + rawLevel.Get % 1_000;
        var config = new SeasonProgressionConfig();
        var threshold = config.XpForLevel(level);
        var actualLevel = config.LevelForXp(threshold);
        var previousLevel = config.LevelForXp(Math.Max(0, threshold - 1));

        return (actualLevel == level
                && previousLevel == Math.Max(1, level - 1))
            .ToProperty()
            .Label($"level={level}, threshold={threshold}, actual={actualLevel}, previous={previousLevel}");
    }

    [Property(MaxTest = 100)]
    public Property SeasonProgression_JsonIntegersAreClamped(int raw)
    {
        var json = $"{{\"xp\":{{\"play\":{raw},\"win\":\"{raw}\"}}}}";
        var config = SeasonProgressionConfig.FromJson(json);
        var expectedPlay = Math.Clamp(raw, 0, 10_000);
        var expectedWin = Math.Clamp(raw, 0, 10_000);

        return (config.PlayXp == expectedPlay && config.WinXp == expectedWin)
            .ToProperty()
            .Label($"raw={raw}, play={config.PlayXp}, win={config.WinXp}");
    }

    [Property(MaxTest = 100)]
    public Property SeasonPlanFactory_ProducesContiguousClampedPlans(
        NonNegativeInt rawCount,
        NonNegativeInt rawDuration,
        PositiveInt rawStartNumber)
    {
        var count = Math.Clamp(rawCount.Get, 1, 100);
        var duration = Math.Clamp(rawDuration.Get, 1, 365);
        var startNumber = 1 + rawStartNumber.Get % 10_000;
        var first = new DateTimeOffset(2026, 7, 28, 0, 0, 0, TimeSpan.Zero);
        var plans = SeasonPlanFactory.CreatePlans(first, rawCount.Get, rawDuration.Get, startNumber);

        var contiguous = plans.Count == count
            && plans[0].StartsAt == first;
        for (var index = 0; contiguous && index < plans.Count; index++)
        {
            var plan = plans[index];
            contiguous = plan.Name == SeasonPlanFactory.NameFor(startNumber + index)
                && plan.EndsAt - plan.StartsAt == TimeSpan.FromDays(duration)
                && (index == 0 || plan.StartsAt == plans[index - 1].EndsAt);
        }

        return contiguous
            .ToProperty()
            .Label($"count={rawCount.Get}, duration={rawDuration.Get}, start={rawStartNumber.Get}, actual={plans.Count}");
    }

    [Property(MaxTest = 100)]
    public Property SeasonRewards_OutOfRangePlacesReturnZero(NonNegativeInt rawPlace)
    {
        var place = rawPlace.Get % 100;
        var config = new SeasonRewardsConfig();
        var expected = place is >= 1 and <= 3 ? config.PlayerTop[place - 1] : 0;

        return (config.PlayerRewardForPlace(place) == expected
                && config.ClanRewardForPlace(place) == (place is >= 1 and <= 3 ? config.ClanTop[place - 1] : 0))
            .ToProperty()
            .Label($"place={place}, player={config.PlayerRewardForPlace(place)}");
    }

    [Property(MaxTest = 100)]
    public Property SeasonRewardsConfig_JsonKeepsOnlyPositiveRewards(NonNegativeInt rawReward)
    {
        var reward = rawReward.Get;
        var config = SeasonRewardsConfig.FromJson(
            $"{{\"rewards\":{{\"playerTop\":[{reward},-1,123],\"clanTop\":[0,456]}}}}");

        return (config.PlayerRewardForPlace(1) == (reward > 0 ? reward : 123)
                && config.PlayerRewardForPlace(2) == (reward > 0 ? 123 : 0)
                && config.ClanRewardForPlace(1) == 456
                && config.ClanRewardForPlace(2) == 0)
            .ToProperty()
            .Label($"reward={reward}, player={string.Join(',', config.PlayerTop)}, clan={string.Join(',', config.ClanTop)}");
    }

    [Property(MaxTest = 100)]
    public Property GameStreakRegistry_OnlyUnlocksReachedMilestones(NonNegativeInt rawStreak)
    {
        var currentStreak = rawStreak.Get % 30;
        var streak = new GameStreak(
            7,
            100,
            42,
            MiniGameIds.Dice,
            currentStreak,
            currentStreak,
            currentStreak,
            new DateOnly(2026, 7, 28),
            DateTimeOffset.UnixEpoch);
        var achievements = GameStreakRegistry.Evaluate(streak);
        var expected = GameStreakRegistry.AchievementDays.Count(days => currentStreak >= days);

        return (GameStreakRegistry.Supports(MiniGameIds.Dice)
                && !GameStreakRegistry.Supports("unknown")
                && achievements.Count == expected
                && achievements.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == achievements.Count
                && achievements.All(x => x.Id.StartsWith("streak_dice_", StringComparison.Ordinal)))
            .ToProperty()
            .Label($"streak={currentStreak}, achievements={string.Join(',', achievements.Select(x => x.Id))}");
    }

    [Property(MaxTest = 100)]
    public Property GameStreakRegistry_ActiveStreakResetsOnlyAfterOneMissedDay(NonNegativeInt rawGap)
    {
        var gap = rawGap.Get % 365;
        var lastPlayed = new DateOnly(2026, 7, 28);
        var today = lastPlayed.AddDays(gap);
        var active = GameStreakRegistry.ActiveStreak(5, lastPlayed, today);

        return (active == (gap > 1 ? 0 : 5))
            .ToProperty()
            .Label($"gap={gap}, active={active}");
    }

    [Property(MaxTest = 100)]
    public Property GameStreakRegistry_PlayDayClampsTimezoneOffset(
        NonNegativeInt rawMinutes,
        NonNegativeInt rawOffset)
    {
        var occurredAt = DateTimeOffset.UnixEpoch
            .AddMinutes(rawMinutes.Get % 1_000_000)
            .ToUnixTimeMilliseconds();
        var offset = rawOffset.Get;
        var expectedOffset = Math.Clamp(offset, -14, 14);
        var expected = DateOnly.FromDateTime(
            DateTimeOffset.FromUnixTimeMilliseconds(occurredAt)
                .ToOffset(TimeSpan.FromHours(expectedOffset))
                .DateTime);

        return (GameStreakRegistry.PlayDay(occurredAt, offset) == expected)
            .ToProperty()
            .Label($"occurredAt={occurredAt}, offset={offset}, expectedOffset={expectedOffset}");
    }

    [Property(MaxTest = 100)]
    public Property AchievementRegistry_OutputIdsAreKnownAndUnique(
        NonNegativeInt rawGames,
        NonNegativeInt rawWins,
        NonNegativeInt rawStake,
        NonNegativeInt rawPayout)
    {
        var player = new SeasonPlayer(
            7,
            100,
            42,
            "Alice",
            rawGames.Get,
            1,
            1_000,
            rawGames.Get % 60,
            rawWins.Get % 60,
            0,
            rawStake.Get,
            rawPayout.Get,
            DateTimeOffset.UnixEpoch);
        var ev = new GameCompletedMetaEvent(
            100,
            42,
            "Alice",
            MiniGameIds.Dice,
            10,
            rawPayout.Get,
            rawPayout.Get > 0,
            1,
            DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds());
        var allIds = AchievementRegistry.GetAll(1_000, 1_000)
            .Select(x => x.Id)
            .ToHashSet(StringComparer.Ordinal);
        var unlocked = AchievementRegistry.Evaluate(ev, player);

        return (unlocked.Select(x => x.Id).Distinct(StringComparer.Ordinal).Count() == unlocked.Count
                && unlocked.All(x => allIds.Contains(x.Id))
                && (!unlocked.Any(x => x.Id == "first_game") || player.GamesPlayed >= 1)
                && (!unlocked.Any(x => x.Id == "first_win") || player.Wins >= 1)
                && (!unlocked.Any(x => x.Id == "ten_games") || player.GamesPlayed >= 10)
                && (!unlocked.Any(x => x.Id == "ten_wins") || player.Wins >= 10)
                && (!unlocked.Any(x => x.Id == "high_roller") || player.TotalStaked >= 1_000)
                && (!unlocked.Any(x => x.Id == "big_payout") || ev.Payout >= 1_000)
                && (!unlocked.Any(x => x.Id == "dice_player") || ev.GameKey == MiniGameIds.Dice))
            .ToProperty()
            .Label($"games={player.GamesPlayed}, wins={player.Wins}, stake={player.TotalStaked}, payout={ev.Payout}");
    }

    [Property(MaxTest = 100)]
    public Property QuestCatalog_SelectionIsDeterministicAndUnique(
        NonNegativeInt rawChat,
        NonNegativeInt rawUser,
        NonNegativeInt rawDay)
    {
        var season = new MetaSeason(
            7,
            "Season 7",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddDays(14),
            "active",
            "{}");
        var now = DateTimeOffset.UnixEpoch.AddDays(rawDay.Get % 3_000);
        var progress = new QuestPlayerProgress(rawDay.Get % 30, rawDay.Get % 100, rawChat.Get);
        var catalog = JsonQuestCatalog.Default;
        var first = catalog.ActiveFor(season, rawChat.Get, rawUser.Get, now, progress);
        var second = catalog.ActiveFor(season, rawChat.Get, rawUser.Get, now, progress);

        return (first.Select(x => x.Id).SequenceEqual(second.Select(x => x.Id))
                && first.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() == first.Count
                && first.All(x => x.Target > 0
                    && (x.Period is "daily" or "weekly")
                    && (x.Rarity is "common" or "uncommon" or "rare" or "epic" or "legendary")))
            .ToProperty()
            .Label($"chat={rawChat.Get}, user={rawUser.Get}, day={rawDay.Get}, selected={first.Count}");
    }

    [Property(MaxTest = 100)]
    public Property QuestCatalog_DeltasAreNonNegativeAndBounded(
        NonNegativeInt rawStake,
        NonNegativeInt rawPayout)
    {
        var stake = (long)rawStake.Get;
        var payout = (long)rawPayout.Get;
        var ev = new GameCompletedMetaEvent(100, 42, "Alice", MiniGameIds.Dice, stake, payout, true, 2, 0);
        var volume = new QuestTemplate("volume", "", "", "daily", "volume", null, 1, 0, 0);
        var payoutQuest = volume with { Id = "payout", Kind = "payout" };
        var profitQuest = volume with { Id = "profit", Kind = "profit" };

        return (JsonQuestCatalog.DeltaFor(volume, ev) == Math.Min(int.MaxValue, stake)
                && JsonQuestCatalog.DeltaFor(payoutQuest, ev) == Math.Min(int.MaxValue, payout)
                && JsonQuestCatalog.DeltaFor(profitQuest, ev) == Math.Min(int.MaxValue, Math.Max(0, payout - stake))
                && JsonQuestCatalog.DeltaFor(volume with { Kind = "play" }, ev) == 1)
            .ToProperty()
            .Label($"stake={stake}, payout={payout}");
    }

    [Property(MaxTest = 100)]
    public Property SeasonQuestRotation_NormalizesSupportedValues(NonNegativeInt raw)
    {
        var focusValues = new[] { "ALL_ROUND", " Daily ", "weekly", "VOLUME", "payout", "clans", "tournaments" };
        var rarityValues = new[] { "NORMAL", " uncommon ", "RARE", "epic" };
        var focus = focusValues[raw.Get % focusValues.Length];
        var rarity = rarityValues[raw.Get % rarityValues.Length];
        var season = new MetaSeason(
            7,
            "Season 7",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddDays(14),
            "active",
            $"{{\"quests\":{{\"focus\":\"{focus}\",\"rarityBias\":\"{rarity}\"}}}}");
        var rotation = SeasonQuestRotationConfig.FromSeason(season);

        return (rotation.Focus == focus.Trim().ToLowerInvariant().Replace('_', '-')
                && rotation.RarityBias == rarity.Trim().ToLowerInvariant())
            .ToProperty()
            .Label($"focus={focus}, rarity={rarity}, actual={rotation.Focus}/{rotation.RarityBias}");
    }

    [Property(MaxTest = 100)]
    public Property QuestCatalog_PeriodKeysAreStable(NonNegativeInt rawDay)
    {
        var now = DateTimeOffset.UnixEpoch.AddDays(rawDay.Get % 3_000);
        var daily = JsonQuestCatalog.PeriodKey("daily", now);
        var weekly = JsonQuestCatalog.PeriodKey("weekly", now);

        return (daily == now.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                && daily == JsonQuestCatalog.PeriodKey("DAILY", now)
                && weekly == JsonQuestCatalog.PeriodKey("WEEKLY", now))
            .ToProperty()
            .Label($"now={now:O}, daily={daily}, weekly={weekly}");
    }
}
