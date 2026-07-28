using Games.Meta.Infrastructure.Persistence;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class MetaApplicationServiceTests
{
    [Fact]
    public async Task MetaService_ForwardsEveryOperationToStore()
    {
        var store = new MetaStoreStub();
        var service = new MetaService(store);
        var ct = new CancellationTokenSource().Token;
        var achievements = new[] { new AchievementDefinition("a1", "First", "Desc", "general", true, false) };
        var playedOn = new DateOnly(2026, 7, 28);

        var season = await service.GetActiveSeasonAsync(ct);
        var profile = await service.GetProfileAsync(100, 42, "Alice", ct);
        var top = await service.GetTopAsync(100, 15, ct);
        var unlocked = await service.GetAchievementsAsync(100, 42, ct);
        var streak = await service.RecordGamePlayedAsync(7, 100, 42, "dice", playedOn, ct);
        var streaks = await service.GetGameStreaksAsync(100, 42, ct);
        var completed = await service.ApplyGameCompletedAsync(100, 42, "Alice", 10, 20, true, ct);
        var xp = await service.AddSeasonXpAsync(7, 100, 42, "Alice", 125, ct);
        var unlockedNow = await service.UnlockAchievementsAsync(7, 100, 42, achievements, ct);

        Assert.Same(store.Season, season);
        Assert.Same(store.Profile, profile);
        Assert.Same(store.Top, top);
        Assert.Same(store.Achievements, unlocked);
        Assert.Same(store.RecordResult, streak);
        Assert.Same(store.Streaks, streaks);
        Assert.Same(store.Completed, completed);
        Assert.Same(store.XpPlayer, xp);
        Assert.Same(store.Unlocks, unlockedNow);

        Assert.Equal((100L, 42L, "Alice"), store.LastProfile);
        Assert.Equal((100L, 15), store.LastTop);
        Assert.Equal((7L, 100L, 42L, "dice", playedOn), store.LastRecord);
        Assert.Equal((100L, 42L, "Alice", 10L, 20L, true), store.LastCompleted);
        Assert.Equal((7L, 100L, 42L, "Alice", 125L), store.LastXp);
        Assert.Equal((7L, 100L, 42L), store.LastUnlock);
    }

    private sealed class MetaStoreStub : IMetaStore
    {
        public MetaSeason Season { get; } = new(7, "Season 7", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "active", "{}");
        public IReadOnlyList<SeasonLeaderboardEntry> Top { get; } = [];
        public IReadOnlyList<PlayerAchievementView> Achievements { get; } = [];
        public GameStreakRecordResult? RecordResult { get; } = null;
        public IReadOnlyList<PlayerGameStreakView> Streaks { get; } = [];
        public SeasonPlayer Completed { get; } = new(7, 100, 42, "Alice", 100, 2, 1000, 1, 1, 0, 10, 20, DateTimeOffset.UnixEpoch);
        public SeasonProfile Profile { get; } = new(
            new MetaSeason(7, "Season 7", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "active", "{}"),
            new SeasonPlayer(7, 100, 42, "Alice", 100, 2, 1000, 1, 1, 0, 10, 20, DateTimeOffset.UnixEpoch),
            "bronze",
            200,
            100);
        public SeasonPlayer XpPlayer { get; } = new(7, 100, 42, "Alice", 125, 2, 1000, 1, 1, 0, 10, 20, DateTimeOffset.UnixEpoch);
        public IReadOnlyList<AchievementUnlock> Unlocks { get; } = [];

        public (long ChatId, long UserId, string DisplayName) LastProfile { get; private set; }
        public (long ChatId, int Limit) LastTop { get; private set; }
        public (long SeasonId, long ChatId, long UserId, string GameKey, DateOnly PlayedOn) LastRecord { get; private set; }
        public (long ChatId, long UserId, string DisplayName, long Stake, long Payout, bool IsWin) LastCompleted { get; private set; }
        public (long SeasonId, long ChatId, long UserId, string DisplayName, long XpDelta) LastXp { get; private set; }
        public (long SeasonId, long ChatId, long UserId) LastUnlock { get; private set; }

        public Task<MetaSeason> GetOrCreateActiveSeasonAsync(CancellationToken ct) => Task.FromResult(Season);

        public Task<SeasonPlayer> EnsurePlayerAsync(MetaSeason season, long chatId, long userId, string displayName, CancellationToken ct) =>
            Task.FromResult(Completed);

        public Task<SeasonPlayer> ApplyGameCompletedAsync(long chatId, long userId, string displayName, long stake, long payout, bool isWin, CancellationToken ct)
        {
            LastCompleted = (chatId, userId, displayName, stake, payout, isWin);
            return Task.FromResult(Completed);
        }

        public Task<SeasonPlayer> AddSeasonXpAsync(long seasonId, long chatId, long userId, string displayName, long xpDelta, CancellationToken ct)
        {
            LastXp = (seasonId, chatId, userId, displayName, xpDelta);
            return Task.FromResult(XpPlayer);
        }

        public Task<IReadOnlyList<AchievementUnlock>> UnlockAchievementsAsync(long seasonId, long chatId, long userId, IEnumerable<AchievementDefinition> achievements, CancellationToken ct)
        {
            LastUnlock = (seasonId, chatId, userId);
            return Task.FromResult(Unlocks);
        }

        public Task<IReadOnlyList<PlayerAchievementView>> GetAchievementsAsync(long chatId, long userId, CancellationToken ct) => Task.FromResult(Achievements);

        public Task<GameStreakRecordResult?> RecordGamePlayedAsync(long seasonId, long chatId, long userId, string gameKey, DateOnly playedOn, CancellationToken ct)
        {
            LastRecord = (seasonId, chatId, userId, gameKey, playedOn);
            return Task.FromResult(RecordResult);
        }

        public Task<IReadOnlyList<PlayerGameStreakView>> GetGameStreaksAsync(long chatId, long userId, CancellationToken ct) => Task.FromResult(Streaks);

        public Task<SeasonProfile> GetProfileAsync(long chatId, long userId, string displayName, CancellationToken ct)
        {
            LastProfile = (chatId, userId, displayName);
            return Task.FromResult(Profile);
        }

        public Task<IReadOnlyList<SeasonLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct)
        {
            LastTop = (chatId, limit);
            return Task.FromResult(Top);
        }
    }
}
