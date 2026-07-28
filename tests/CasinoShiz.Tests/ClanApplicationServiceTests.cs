using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Games.Meta.Infrastructure.History;
using Games.Meta.Infrastructure.Persistence;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class ClanApplicationServiceTests
{
    [Fact]
    public async Task CreateAndJoin_SuccessfulMutationsAppendHistory()
    {
        var clan = Clan(7, "TAG", "Red Dragons");
        var fixture = CreateFixture();
        fixture.Clans.CreateResult = new ClanCreateResult(true, "created", clan);
        fixture.Clans.JoinResult = new ClanJoinResult(true, "joined", clan);

        var created = await fixture.Service.CreateAsync(100, 42, "Alice", "TAG", "Red Dragons", CancellationToken.None);
        var joined = await fixture.Service.JoinAsync(100, 43, "Bob", "TAG", CancellationToken.None);

        Assert.True(created.Created);
        Assert.True(joined.Joined);
        Assert.Equal(2, fixture.Meta.ActiveSeasonCalls);
        Assert.Equal(2, fixture.History.AppendCalls);
        Assert.Equal("clan.joined", fixture.History.LastEventType);
        Assert.Equal("7", fixture.History.LastAggregateId);
    }

    [Fact]
    public async Task FailedMutations_DoNotAppendHistory()
    {
        var fixture = CreateFixture();
        fixture.Clans.CreateResult = new ClanCreateResult(false, "already exists");
        fixture.Clans.JoinResult = new ClanJoinResult(false, "not found");

        await fixture.Service.CreateAsync(100, 42, "Alice", "TAG", "Red Dragons", CancellationToken.None);
        await fixture.Service.JoinAsync(100, 42, "Alice", "TAG", CancellationToken.None);

        Assert.Equal(0, fixture.History.AppendCalls);
    }

    [Fact]
    public async Task Reads_UseActiveSeasonExceptMembers()
    {
        var fixture = CreateFixture();
        var clan = Clan(7, "TAG", "Red Dragons");
        fixture.Clans.UserClan = clan;
        fixture.Clans.ByTag = clan;
        fixture.Clans.Members = [new ClanMemberInfo(7, 42, "Alice", "owner", DateTimeOffset.UnixEpoch)];
        fixture.Clans.Top = [new ClanLeaderboardEntry(1, 7, "Red Dragons", "TAG", 1, 100, 10)];

        var userClan = await fixture.Service.GetUserClanAsync(100, 42, CancellationToken.None);
        var byTag = await fixture.Service.GetClanByTagAsync(100, "TAG", CancellationToken.None);
        var members = await fixture.Service.GetMembersAsync(7, CancellationToken.None);
        var top = await fixture.Service.GetTopAsync(100, 15, CancellationToken.None);

        Assert.Same(clan, userClan);
        Assert.Same(clan, byTag);
        Assert.Single(members);
        Assert.Single(top);
        Assert.Equal(3, fixture.Meta.ActiveSeasonCalls);
        Assert.Equal((100L, 15), fixture.Clans.LastTop);
    }

    [Fact]
    public async Task ApplyGameCompleted_CalculatesXpAndLogsOnlyForClanMember()
    {
        var fixture = CreateFixture();
        fixture.Clans.UserClan = Clan(7, "TAG", "Red Dragons");
        var win = new GameCompletedMetaEvent(100, 42, "Alice", "dice", 10_000, 20_000, true, 2, 1000);

        await fixture.Service.ApplyGameCompletedAsync(win, CancellationToken.None);

        Assert.Equal(60, fixture.Clans.LastXpDelta);
        Assert.Equal(1, fixture.History.AppendCalls);
        Assert.Equal("clan.progressed", fixture.History.LastEventType);

        fixture.Clans.UserClan = null;
        var loss = win with { Stake = -10_000, IsWin = false };
        await fixture.Service.ApplyGameCompletedAsync(loss, CancellationToken.None);

        Assert.Equal(3, fixture.Clans.LastXpDelta);
        Assert.Equal(1, fixture.History.AppendCalls);
    }

    [Property(MaxTest = 100)]
    public async Task<Property> ClanXp_IsBoundedAndMonotonic(NonNegativeInt rawStake, NonNegativeInt rawDelta)
    {
        var stake = Math.Min(rawStake.Get, int.MaxValue - rawDelta.Get);
        var nextStake = stake + rawDelta.Get;
        var baseXp = 10L;
        var first = await CaptureXpAsync(stake, isWin: true);
        var second = await CaptureXpAsync(nextStake, isWin: true);

        return (first >= baseXp && first <= 250 && second >= baseXp && second <= 250 && first <= second)
            .ToProperty()
            .Label($"stake={stake}, nextStake={nextStake}, first={first}, second={second}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> ClanXp_LossBaseIsNeverBelowThree(NonNegativeInt rawStake)
    {
        var stake = rawStake.Get;
        var xp = await CaptureXpAsync(stake, isWin: false);

        return (xp >= 3 && xp <= 250)
            .ToProperty()
            .Label($"stake={stake}, xp={xp}");
    }

    private static async Task<long> CaptureXpAsync(long stake, bool isWin)
    {
        var fixture = CreateFixture();
        fixture.Clans.UserClan = null;
        await fixture.Service.ApplyGameCompletedAsync(
            new GameCompletedMetaEvent(100, 42, "Alice", "dice", stake, 0, isWin, 1, 1000),
            CancellationToken.None);
        return fixture.Clans.LastXpDelta;
    }

    private static Fixture CreateFixture() => new(new ActiveSeasonMetaStub(), new ClanStoreStub(), new HistoryStoreStub());

    private static ClanInfo Clan(long id, string tag, string name) =>
        new(id, 100, name, tag, 42, DateTimeOffset.UnixEpoch, 2, 100, 10);

    private sealed class Fixture
    {
        public Fixture(ActiveSeasonMetaStub meta, ClanStoreStub clans, HistoryStoreStub history)
        {
            Meta = meta;
            Clans = clans;
            History = history;
            Service = new ClanService(meta, clans, history);
        }

        public ClanService Service { get; }
        public ActiveSeasonMetaStub Meta { get; }
        public ClanStoreStub Clans { get; }
        public HistoryStoreStub History { get; }
    }

    private sealed class ActiveSeasonMetaStub : IMetaService
    {
        public int ActiveSeasonCalls { get; private set; }
        public MetaSeason Season { get; } = new(7, "Season 7", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "active", "{}");

        public Task<MetaSeason> GetActiveSeasonAsync(CancellationToken ct)
        {
            ActiveSeasonCalls++;
            return Task.FromResult(Season);
        }

        public Task<SeasonProfile> GetProfileAsync(long chatId, long userId, string displayName, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SeasonLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerAchievementView>> GetAchievementsAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<GameStreakRecordResult?> RecordGamePlayedAsync(long seasonId, long chatId, long userId, string gameKey, DateOnly playedOn, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerGameStreakView>> GetGameStreaksAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<SeasonPlayer> ApplyGameCompletedAsync(long chatId, long userId, string displayName, long stake, long payout, bool isWin, CancellationToken ct) => throw new NotSupportedException();
        public Task<SeasonPlayer> AddSeasonXpAsync(long seasonId, long chatId, long userId, string displayName, long xpDelta, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AchievementUnlock>> UnlockAchievementsAsync(long seasonId, long chatId, long userId, IEnumerable<AchievementDefinition> achievements, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class ClanStoreStub : IClanStore
    {
        public ClanCreateResult CreateResult { get; set; } = new(false, "create");
        public ClanJoinResult JoinResult { get; set; } = new(false, "join");
        public ClanInfo? UserClan { get; set; }
        public ClanInfo? ByTag { get; set; }
        public IReadOnlyList<ClanMemberInfo> Members { get; set; } = [];
        public IReadOnlyList<ClanLeaderboardEntry> Top { get; set; } = [];
        public long LastXpDelta { get; private set; }
        public (long ChatId, int Limit) LastTop { get; private set; }

        public Task<ClanCreateResult> CreateAsync(MetaSeason season, long chatId, long userId, string displayName, string tag, string name, CancellationToken ct) => Task.FromResult(CreateResult);
        public Task<ClanJoinResult> JoinAsync(MetaSeason season, long chatId, long userId, string displayName, string tag, CancellationToken ct) => Task.FromResult(JoinResult);
        public Task<ClanInfo?> GetUserClanAsync(MetaSeason season, long chatId, long userId, CancellationToken ct) => Task.FromResult(UserClan);
        public Task<ClanInfo?> GetClanByTagAsync(MetaSeason season, long chatId, string tag, CancellationToken ct) => Task.FromResult(ByTag);
        public Task<IReadOnlyList<ClanMemberInfo>> GetMembersAsync(long clanId, CancellationToken ct) => Task.FromResult(Members);

        public Task<IReadOnlyList<ClanLeaderboardEntry>> GetTopAsync(MetaSeason season, long chatId, int limit, CancellationToken ct)
        {
            LastTop = (chatId, limit);
            return Task.FromResult(Top);
        }

        public Task ApplyGameCompletedAsync(MetaSeason season, GameCompletedMetaEvent ev, long xpDelta, CancellationToken ct)
        {
            LastXpDelta = xpDelta;
            return Task.CompletedTask;
        }
    }

    private sealed class HistoryStoreStub : IMetaHistoryStore
    {
        public int AppendCalls { get; private set; }
        public string? LastEventType { get; private set; }
        public string? LastAggregateId { get; private set; }

        public Task AppendAsync(string eventType, string aggregateType, string aggregateId, long? seasonId, long? chatId, long? userId, object payload, CancellationToken ct)
        {
            AppendCalls++;
            LastEventType = eventType;
            LastAggregateId = aggregateId;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MetaHistoryEvent>> ListAsync(string? eventType, string? aggregateType, string? aggregateId, long? chatId, long? userId, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<MetaHistoryStats> GetStatsAsync(CancellationToken ct) => throw new NotSupportedException();
    }
}
