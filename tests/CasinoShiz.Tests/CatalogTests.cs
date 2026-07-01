using Xunit;

namespace CasinoShiz.Tests;

public sealed class CatalogTests
{
    [Theory]
    [InlineData("dice", ChallengeGame.DiceCube, "🎲", "dicecube")]
    [InlineData("dicecube", ChallengeGame.DiceCube, "🎲", "dicecube")]
    [InlineData("🎲", ChallengeGame.DiceCube, "🎲", "dicecube")]
    [InlineData("дартс", ChallengeGame.Darts, "🎯", "darts")]
    [InlineData("боулинг", ChallengeGame.Bowling, "🎳", "bowling")]
    [InlineData("basketball", ChallengeGame.Basketball, "🏀", "basketball")]
    [InlineData("⚽", ChallengeGame.Football, "⚽", "football")]
    [InlineData("слоты", ChallengeGame.Slots, "🎰", "slots")]
    [InlineData("🐎", ChallengeGame.Horse, "🐎", "horse")]
    [InlineData("bj", ChallengeGame.Blackjack, "🃏", "blackjack")]
    public void ChallengeGameCatalog_TryParse_UnderstandsAliases(
        string value,
        ChallengeGame expectedGame,
        string expectedEmoji,
        string expectedName)
    {
        Assert.True(ChallengeGameCatalog.TryParse(value, out var parsed));
        Assert.Equal(expectedGame, parsed);
        Assert.Equal(expectedEmoji, ChallengeGameCatalog.Emoji(parsed));
        Assert.Equal(expectedName, ChallengeGameCatalog.DisplayName(parsed));
    }

    [Fact]
    public void ChallengeGameCatalog_TryParse_RejectsUnknownValues()
    {
        Assert.False(ChallengeGameCatalog.TryParse("???", out var parsed));
        Assert.Equal(default, parsed);
        Assert.Equal("🎲", ChallengeGameCatalog.Emoji(default));
    }

    [Fact]
    public void JsonQuestCatalog_ValidateJson_BuildsExpandedStateFromCompactDocument()
    {
        const string json = """
            {
              "games": [
                { "key": "dice", "title": "Dice", "command": "/dice" }
              ],
              "slots": [
                { "id": "daily_demo", "period": "daily", "poolTags": ["starter"], "count": 1, "repeatCooldownPeriods": 0 },
                { "id": "weekly_demo", "period": "weekly", "poolTags": ["risk"], "count": 1, "repeatCooldownPeriods": 0 }
              ],
              "definitions": [
                {
                  "id": "base_play",
                  "period": "daily",
                  "kind": "play",
                  "cluster": "Starter Pack",
                  "rarity": "mystery",
                  "tags": ["starter", "easy", "starter"],
                  "rewardXp": 10,
                  "rewardCoins": 20
                },
                {
                  "id": "rich_daily",
                  "period": "daily",
                  "kind": "volume",
                  "cluster": "High Risk",
                  "rarity": "EPIC",
                  "tags": ["risk"],
                  "gameKeys": ["dice"],
                  "targets": [100, 200],
                  "minStakes": [50],
                  "maxStakes": [250],
                  "minPayouts": [300],
                  "minProfits": [75],
                  "minMultipliers": [1.5],
                  "rewardXp": 50,
                  "rewardCoins": 60,
                  "titles": ["Turn {target}", "Turn {game}"],
                  "descriptions": ["Stake {minStake}", "Command {gameCommand}"]
                },
                {
                  "id": "weekly_win",
                  "period": "weekly",
                  "kind": "win",
                  "cluster": "",
                  "rarity": "rare",
                  "tags": ["game"],
                  "gameKeys": ["dice"],
                  "rewardXp": 30,
                  "rewardCoins": 40,
                  "titles": ["Weekly {target}"],
                  "descriptions": ["Beat {game}"]
                }
              ]
            }
            """;

        var validation = JsonQuestCatalog.ValidateJson(json);

        Assert.Equal(42, validation.QuestCount);
        Assert.Equal(2, validation.SlotCount);
        Assert.Equal(3, validation.DefinitionCount);
        Assert.Equal(41, validation.DailyQuestCount);
        Assert.Equal(1, validation.WeeklyQuestCount);
    }

    [Fact]
    public void JsonQuestCatalog_ValidateJson_DetectsDuplicateIds()
    {
        const string json = """
            {
              "games": [
                { "key": "dice", "title": "Dice", "command": "/dice" }
              ],
              "slots": [
                { "id": "daily_demo", "period": "daily", "poolTags": ["starter"], "count": 1, "repeatCooldownPeriods": 0 }
              ],
              "definitions": [
                { "id": "dup", "period": "daily", "kind": "play", "rewardXp": 1, "rewardCoins": 1 },
                { "id": "dup", "period": "daily", "kind": "play", "rewardXp": 1, "rewardCoins": 1 }
              ]
            }
            """;

        var ex = Assert.Throws<InvalidOperationException>(() => JsonQuestCatalog.ValidateJson(json));
        Assert.Contains("Duplicate quest id", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void JsonQuestCatalog_ValidateJson_DetectsUnknownGameReference()
    {
        const string json = """
            {
              "games": [
                { "key": "dice", "title": "Dice", "command": "/dice" }
              ],
              "slots": [
                { "id": "daily_demo", "period": "daily", "poolTags": ["starter"], "count": 1, "repeatCooldownPeriods": 0 }
              ],
              "definitions": [
                {
                  "id": "bad",
                  "period": "daily",
                  "kind": "play",
                  "gameKeys": ["unknown"],
                  "rewardXp": 1,
                  "rewardCoins": 1
                }
              ]
            }
            """;

        var ex = Assert.Throws<InvalidOperationException>(() => JsonQuestCatalog.ValidateJson(json));
        Assert.Contains("references unknown game", ex.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("daily", "daily")]
    [InlineData("weekly", "weekly")]
    [InlineData("monthly", "daily")]
    public void JsonQuestCatalog_PeriodKey_NormalizesPeriods(string input, string expected)
    {
        var quest = new QuestTemplate("q", "t", "d", input, "play", GameKey: null, 1, 1, 1);
        var now = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);

        var key = JsonQuestCatalog.PeriodKey(quest, now);

        if (string.Equals(expected, "weekly", StringComparison.Ordinal))
            Assert.Equal("2026-W26", key);
        else
            Assert.Equal("2026-06-24", key);
    }

    [Theory]
    [InlineData("daily", "daily")]
    [InlineData("weekly", "weekly")]
    public void QuestRegistry_ActiveFor_PeriodFocusKeepsPeriods(
        string focus,
        string expectedPeriod)
    {
        var season = SeasonWithFocus(focus, rarityBias: "normal");
        var progress = new QuestPlayerProgress(100, 1_000, 1_000_000);
        var now = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);

        var active = QuestRegistry.ActiveFor(season, 100, 42, now, progress);

        Assert.NotEmpty(active);
        Assert.Contains(active, q => string.Equals(q.Period, expectedPeriod, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("volume", "volume|high_stake")]
    [InlineData("payout", "payout|profit|multiplier|high_stake")]
    [InlineData("controlled", "low_stake|play|loss|high_stake")]
    public void QuestRegistry_ActiveFor_FocusBiasesKinds(
        string focus,
        string allowedKinds)
    {
        var season = SeasonWithFocus(focus, rarityBias: "normal");
        var progress = new QuestPlayerProgress(100, 1_000, 1_000_000);
        var now = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);

        var active = QuestRegistry.ActiveFor(season, 100, 42, now, progress);
        var allowed = allowedKinds.Split('|', StringSplitOptions.RemoveEmptyEntries);

        Assert.NotEmpty(active);
        Assert.Contains(active, q => allowed.Contains(q.Kind, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("common")]
    [InlineData("uncommon")]
    [InlineData("rare")]
    [InlineData("epic")]
    public void QuestRegistry_ActiveFor_RarityBiases_ReturnQuests(string rarityBias)
    {
        var season = SeasonWithFocus("daily", rarityBias);
        var progress = new QuestPlayerProgress(100, 1_000, 1_000_000);
        var now = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);

        var active = QuestRegistry.ActiveFor(season, 100, 42, now, progress);

        Assert.NotEmpty(active);
    }

    [Fact]
    public void QuestRegistry_Matching_RespectsVolumeOutcomeKinds()
    {
        var season = SeasonWithFocus("volume", "normal");
        var now = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
        var progress = new QuestPlayerProgress(100, 1_000, 1_000_000);

        var ev = new GameCompletedMetaEvent(100, 42, "u", MiniGameIds.Dice, 1_000, 0, IsWin: false, 0, now.ToUnixTimeMilliseconds());
        var matching = QuestRegistry.Matching(season, ev.ChatId, ev.UserId, ev, progress).ToArray();

        Assert.NotEmpty(matching);
        Assert.All(matching, quest =>
        {
            if (quest.GameKey is not null)
                Assert.Equal(MiniGameIds.Dice, quest.GameKey);
            Assert.True(
                quest.Kind is "volume" or "high_stake" or "play" or "low_stake",
                quest.Kind);
        });
    }

    [Fact]
    public void QuestRegistry_Matching_RespectsPayoutOutcomeKinds()
    {
        var season = SeasonWithFocus("payout", "normal");
        var now = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
        var progress = new QuestPlayerProgress(100, 1_000, 1_000_000);

        var ev = new GameCompletedMetaEvent(100, 42, "u", MiniGameIds.Dice, 1_000, 1_600, IsWin: true, 2, now.ToUnixTimeMilliseconds());
        var matching = QuestRegistry.Matching(season, ev.ChatId, ev.UserId, ev, progress).ToArray();

        Assert.NotEmpty(matching);
        Assert.All(matching, quest =>
        {
            if (quest.GameKey is not null)
                Assert.Equal(MiniGameIds.Dice, quest.GameKey);
            Assert.True(
                quest.Kind is "payout" or "profit" or "multiplier" or "play" or "low_stake" or "high_stake" or "win",
                quest.Kind);
        });
    }

    [Fact]
    public void QuestRegistry_Matching_RespectsControlledLossKinds()
    {
        var season = SeasonWithFocus("controlled", "normal");
        var now = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
        var progress = new QuestPlayerProgress(100, 1_000, 1_000_000);

        var ev = new GameCompletedMetaEvent(100, 42, "u", MiniGameIds.Dice, 100, 0, IsWin: false, 0, now.ToUnixTimeMilliseconds());
        var matching = QuestRegistry.Matching(season, ev.ChatId, ev.UserId, ev, progress).ToArray();

        Assert.NotEmpty(matching);
        Assert.All(matching, quest =>
        {
            if (quest.GameKey is not null)
                Assert.Equal(MiniGameIds.Dice, quest.GameKey);
            Assert.True(
                quest.Kind is "low_stake" or "play" or "loss" or "high_stake" or "volume",
                quest.Kind);
        });
    }

    [Fact]
    public void QuestRegistry_FindActive_IsCaseInsensitive()
    {
        var season = SeasonWithFocus("daily", "normal");
        var now = new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero);
        var progress = new QuestPlayerProgress(100, 1_000, 1_000_000);
        var target = QuestRegistry.ActiveFor(season, 100, 42, now, progress)[0];

        var found = JsonQuestCatalog.Default.FindActive(season, 100, 42, target.Id.ToUpperInvariant(), now, progress);

        Assert.NotNull(found);
        Assert.Equal(target.Id, found!.Id);
    }

    [Fact]
    public void QuestRegistry_PeriodKey_WeeklyUsesIsoWeek()
    {
        var quest = QuestRegistry.All.First(x => string.Equals(x.Period, "weekly", StringComparison.Ordinal));
        var key = QuestRegistry.PeriodKey(quest, new DateTimeOffset(2026, 6, 24, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal("2026-W26", key);
    }

    [Fact]
    public void QuestRegistry_DeltaFor_ClampsZeroAndPositiveKinds()
    {
        var ev = new GameCompletedMetaEvent(100, 42, "u", MiniGameIds.Dice, 10, 6, IsWin: true, 2, 1);
        var volume = QuestRegistry.All.First(x => string.Equals(x.Kind, "volume", StringComparison.Ordinal) && string.Equals(x.GameKey, MiniGameIds.Dice, StringComparison.Ordinal));
        var profit = QuestRegistry.All.First(x => string.Equals(x.Kind, "profit", StringComparison.Ordinal));
        var multiplier = QuestRegistry.All.First(x => string.Equals(x.Kind, "multiplier", StringComparison.Ordinal) && string.Equals(x.GameKey, MiniGameIds.Dice, StringComparison.Ordinal));
        var play = QuestRegistry.All.First(x => string.Equals(x.Kind, "play", StringComparison.Ordinal));

        Assert.Equal(10, QuestRegistry.DeltaFor(volume, ev));
        var lowPayout = new GameCompletedMetaEvent(100, 42, "u", MiniGameIds.Dice, 10, 5, IsWin: true, 2, 1);

        Assert.Equal(0, QuestRegistry.DeltaFor(profit, lowPayout));
        Assert.Equal(1, QuestRegistry.DeltaFor(multiplier, ev));
        Assert.Equal(1, QuestRegistry.DeltaFor(play, ev));
    }

    [Fact]
    public void JsonQuestCatalog_DeltaFor_ClampsLargeVolumeAndPayoutToIntMax()
    {
        var volume = new QuestTemplate("v", "v", "v", "daily", "volume", null, 1, 0, 0);
        var payout = new QuestTemplate("p", "p", "p", "daily", "payout", null, 1, 0, 0);
        var ev = new GameCompletedMetaEvent(1, 2, "u", "dice", long.MaxValue, long.MaxValue, true, 1, 1);

        Assert.Equal(int.MaxValue, JsonQuestCatalog.DeltaFor(volume, ev));
        Assert.Equal(int.MaxValue, JsonQuestCatalog.DeltaFor(payout, ev));
    }

    [Fact]
    public void JsonQuestCatalog_ReadEffectiveJson_LoadsValidCatalog()
    {
        var json = JsonQuestCatalog.ReadEffectiveJson();

        var validation = JsonQuestCatalog.ValidateJson(json);
        Assert.True(validation.QuestCount > 0);
        Assert.True(validation.DefinitionCount > 0);
        Assert.False(string.IsNullOrWhiteSpace(JsonQuestCatalog.EditablePath()));
    }

    private static MetaSeason SeasonWithFocus(string focus, string rarityBias) =>
        new(
            7,
            "season",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch.AddDays(14),
            "active",
            $$"""
            {
              "quests": {
                "focus": "{{focus}}",
                "rarityBias": "{{rarityBias}}"
              }
            }
            """);
}
