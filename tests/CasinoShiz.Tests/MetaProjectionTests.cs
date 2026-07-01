using Games.Meta.Application.Meta;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Quests;
using Games.Meta.Domain.Achievements;
using Games.Meta.Domain.Quests;
using Games.Meta.Domain.Seasons;
using Games.Meta.Domain.Streaks;
using Games.Meta.Infrastructure.History;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class MetaProjectionTests
{
    [Fact]
    public async Task ClanProjection_ForwardsGameCompletionToClanService()
    {
        var clans = new ClanServiceStub();
        var projection = new ClanProjection(clans, NullLogger<ClanProjection>.Instance);
        var ev = new GameCompletedMetaEvent(100, 42, "Alice", MiniGameIds.Dice, 50, 125, true, 3, 1234);

        await ((IDomainEventSubscriber)projection).HandleAsync(ev, CancellationToken.None);

        Assert.Same(ev, clans.Applied);
    }

    [Fact]
    public async Task ClanProjection_IgnoresOtherDomainEventTypes()
    {
        var clans = new ClanServiceStub();
        var projection = new ClanProjection(clans, NullLogger<ClanProjection>.Instance);

        await ((IDomainEventSubscriber)projection).HandleAsync(new UnrelatedEvent(), CancellationToken.None);

        Assert.Null(clans.Applied);
    }

    [Fact]
    public async Task QuestProjection_RecordsOnlyCompletedQuestUpdates()
    {
        var season = Season();
        var quests = new QuestServiceStub
        {
            Updates =
            [
                new QuestProgressUpdate("in-progress", 2, 3, Completed: false),
                new QuestProgressUpdate("completed", 5, 5, Completed: true),
            ],
        };
        var history = new HistoryStoreStub();
        var projection = new QuestProjection(
            quests,
            new MetaServiceStub(season),
            history,
            NullLogger<QuestProjection>.Instance);
        var ev = new GameCompletedMetaEvent(100, 42, "Alice", MiniGameIds.Dice, 50, 125, true, 3, 1234);

        await ((IDomainEventSubscriber)projection).HandleAsync(ev, CancellationToken.None);

        var entry = Assert.Single(history.Entries);
        Assert.Equal("quest.progressed", entry.EventType);
        Assert.Equal("player", entry.AggregateType);
        Assert.Equal("7:100:42", entry.AggregateId);
        Assert.Equal(7, entry.SeasonId);
        Assert.Equal(100, entry.ChatId);
        Assert.Equal(42, entry.UserId);
        Assert.Contains("completed", entry.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("in-progress", entry.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QuestProjection_WithNoCompletedUpdates_DoesNotAppendHistory()
    {
        var history = new HistoryStoreStub();
        var projection = new QuestProjection(
            new QuestServiceStub { Updates = [new QuestProgressUpdate("q", 1, 2, false)] },
            new MetaServiceStub(Season()),
            history,
            NullLogger<QuestProjection>.Instance);
        var ev = new GameCompletedMetaEvent(100, 42, "Alice", MiniGameIds.Dice, 10, 0, false, 0, 1);

        await ((IDomainEventSubscriber)projection).HandleAsync(ev, CancellationToken.None);

        Assert.Empty(history.Entries);
    }

    [Fact]
    public async Task QuestProjection_IgnoresOtherDomainEventTypes()
    {
        var quests = new QuestServiceStub { Updates = [new QuestProgressUpdate("q", 1, 1, true)] };
        var history = new HistoryStoreStub();
        var projection = new QuestProjection(quests, new MetaServiceStub(Season()), history, NullLogger<QuestProjection>.Instance);

        await ((IDomainEventSubscriber)projection).HandleAsync(new UnrelatedEvent(), CancellationToken.None);

        Assert.Equal(0, quests.ApplyCalls);
        Assert.Empty(history.Entries);
    }

    private static MetaSeason Season() =>
        new(7, "season", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(7), "active", "{}");

    private sealed record UnrelatedEvent : IDomainEvent
    {
        public string EventType => "test.unrelated";
        public long OccurredAt => 1;
    }

    private sealed class QuestServiceStub : IQuestService
    {
        public IReadOnlyList<QuestProgressUpdate> Updates { get; init; } = [];
        public int ApplyCalls { get; private set; }

        public Task<IReadOnlyList<QuestProgressUpdate>> ApplyGameCompletedAsync(GameCompletedMetaEvent ev, CancellationToken ct)
        {
            ApplyCalls++;
            return Task.FromResult(Updates);
        }

        public Task<IReadOnlyList<PlayerQuestView>> GetQuestsAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<QuestClaimResult?> ClaimAsync(long chatId, long userId, string displayName, string questId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class ClanServiceStub : IClanService
    {
        public GameCompletedMetaEvent? Applied { get; private set; }
        public Task ApplyGameCompletedAsync(GameCompletedMetaEvent ev, CancellationToken ct)
        {
            Applied = ev;
            return Task.CompletedTask;
        }

        public Task<ClanCreateResult> CreateAsync(long chatId, long userId, string displayName, string tag, string name, CancellationToken ct) => throw new NotSupportedException();
        public Task<ClanJoinResult> JoinAsync(long chatId, long userId, string displayName, string tag, CancellationToken ct) => throw new NotSupportedException();
        public Task<ClanInfo?> GetUserClanAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ClanInfo?> GetClanByTagAsync(long chatId, string tag, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ClanMemberInfo>> GetMembersAsync(long clanId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ClanLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class MetaServiceStub(MetaSeason season) : IMetaService
    {
        public Task<MetaSeason> GetActiveSeasonAsync(CancellationToken ct) => Task.FromResult(season);
        public Task<SeasonProfile> GetProfileAsync(long chatId, long userId, string displayName, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SeasonLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerAchievementView>> GetAchievementsAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<GameStreakRecordResult?> RecordGamePlayedAsync(long seasonId, long chatId, long userId, string gameKey, DateOnly playedOn, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerGameStreakView>> GetGameStreaksAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<SeasonPlayer> ApplyGameCompletedAsync(long chatId, long userId, string displayName, long stake, long payout, bool isWin, CancellationToken ct) => throw new NotSupportedException();
        public Task<SeasonPlayer> AddSeasonXpAsync(long seasonId, long chatId, long userId, string displayName, long xpDelta, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AchievementUnlock>> UnlockAchievementsAsync(long seasonId, long chatId, long userId, IEnumerable<AchievementDefinition> achievements, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class HistoryStoreStub : IMetaHistoryStore
    {
        public List<Entry> Entries { get; } = [];

        public Task AppendAsync(string eventType, string aggregateType, string aggregateId, long? seasonId, long? chatId, long? userId, object payload, CancellationToken ct)
        {
            Entries.Add(new Entry(eventType, aggregateType, aggregateId, seasonId, chatId, userId, System.Text.Json.JsonSerializer.Serialize(payload)));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MetaHistoryEvent>> ListAsync(string? eventType, string? aggregateType, string? aggregateId, long? chatId, long? userId, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<MetaHistoryStats> GetStatsAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed record Entry(string EventType, string AggregateType, string AggregateId, long? SeasonId, long? ChatId, long? UserId, string PayloadJson);
}
