using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Games.Meta.Application.Effects;
using Games.Meta.Infrastructure.Persistence;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class QuestApplicationServiceTests
{
    [Fact]
    public async Task ApplyGameCompleted_BuildsStableEnvelopeAndReturnsUpdates()
    {
        var executor = new CapturingEffectExecutor();
        var updates = new[] { new QuestProgressUpdate("quest-1", 2, 5, false) };
        executor.Outputs["updates"] = updates;
        var fixture = CreateFixture(executor);
        var ev = new GameCompletedMetaEvent(100, 42, "Alice", "dice", 10, 20, true, 2, 1_000);

        var result = await fixture.Service.ApplyGameCompletedAsync(ev, CancellationToken.None);

        Assert.Same(updates, result);
        Assert.Equal("meta.quest", executor.Envelope!.GameId);
        Assert.Equal("7:100:42", executor.Envelope.AggregateId);
        Assert.Contains("meta:quest:progress:7:100:42:1000:dice:10:20:True:2", executor.Envelope.CommandId, StringComparison.Ordinal);
        Assert.Equal(3, executor.Envelope.LockKeys.Count);
        var effect = Assert.IsType<QuestProgressAtomicEffect>(Assert.Single(executor.Effects));
        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1_000), effect.Now);
        Assert.Equal(ev, effect.Completion);
    }

    [Fact]
    public async Task Claim_BuildsWalletAndQuestLocksAndUsesResultOutput()
    {
        var executor = new CapturingEffectExecutor();
        var claim = new QuestClaimResult("quest-1", "Quest", 125, 250, true);
        executor.Outputs["result"] = claim;
        var fixture = CreateFixture(executor);

        var result = await fixture.Service.ClaimAsync(100, 42, "Alice", "quest-1", CancellationToken.None);

        Assert.Same(claim, result);
        Assert.Equal("meta:quest:claim:7:100:42:quest-1", executor.Envelope!.CommandId);
        Assert.Contains("wallet:100:42", executor.Envelope.LockKeys, StringComparer.Ordinal);
        var effect = Assert.IsType<QuestClaimAtomicEffect>(Assert.Single(executor.Effects));
        Assert.Equal(7, effect.SeasonId);
        Assert.Equal(100, effect.ChatId);
        Assert.Equal(42, effect.UserId);
        Assert.Equal("Alice", effect.DisplayName);
        Assert.Equal("quest-1", effect.QuestId);
    }

    [Fact]
    public async Task GetQuests_UsesActiveSeasonAndStoreTime()
    {
        var executor = new CapturingEffectExecutor();
        var store = new QuestStoreStub
        {
            Active = [new PlayerQuestView("quest-1", "Quest", "Desc", "daily", 1, 2, false, false, 10, 20)],
        };
        var fixture = CreateFixture(executor, store);

        var result = await fixture.Service.GetQuestsAsync(100, 42, CancellationToken.None);

        Assert.Same(store.Active, result);
        Assert.Equal(7, store.LastSeasonId);
        Assert.Equal((100L, 42L), store.LastPlayer);
        Assert.NotEqual(default, store.LastNow);
    }

    [Property(MaxTest = 100)]
    public async Task<Property> ClaimCommandId_IsDeterministicForIdentity(NonNegativeInt chat, NonNegativeInt user, NonEmptyString quest)
    {
        var executor = new CapturingEffectExecutor();
        var fixture = CreateFixture(executor);
        var questId = quest.Get;

        await fixture.Service.ClaimAsync(chat.Get, user.Get, "Alice", questId, CancellationToken.None);

        var expected = $"meta:quest:claim:7:{chat.Get}:{user.Get}:{questId}";
        return (executor.Envelope!.CommandId == expected
                && executor.Envelope.AggregateId == $"7:{chat.Get}:{user.Get}"
                && executor.Envelope.LockKeys.Contains($"quest:7:{chat.Get}:{user.Get}:{questId}", StringComparer.Ordinal))
            .ToProperty()
            .Label($"chat={chat.Get}, user={user.Get}, quest={questId}");
    }

    private static Fixture CreateFixture(CapturingEffectExecutor executor, QuestStoreStub? store = null) =>
        new(new ActiveSeasonMetaStub(), store ?? new QuestStoreStub(), executor);

    private sealed class Fixture
    {
        public Fixture(ActiveSeasonMetaStub meta, QuestStoreStub quests, CapturingEffectExecutor effects)
        {
            Service = new QuestService(meta, quests, effects);
            Quests = quests;
        }

        public QuestService Service { get; }
        public QuestStoreStub Quests { get; }
    }

    private sealed class ActiveSeasonMetaStub : IMetaService
    {
        public MetaSeason Season { get; } = new(7, "Season 7", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "active", "{}");
        public Task<MetaSeason> GetActiveSeasonAsync(CancellationToken ct) => Task.FromResult(Season);
        public Task<SeasonProfile> GetProfileAsync(long chatId, long userId, string displayName, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<SeasonLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerAchievementView>> GetAchievementsAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<GameStreakRecordResult?> RecordGamePlayedAsync(long seasonId, long chatId, long userId, string gameKey, DateOnly playedOn, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<PlayerGameStreakView>> GetGameStreaksAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<SeasonPlayer> ApplyGameCompletedAsync(long chatId, long userId, string displayName, long stake, long payout, bool isWin, CancellationToken ct) => throw new NotSupportedException();
        public Task<SeasonPlayer> AddSeasonXpAsync(long seasonId, long chatId, long userId, string displayName, long xpDelta, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<AchievementUnlock>> UnlockAchievementsAsync(long seasonId, long chatId, long userId, IEnumerable<AchievementDefinition> achievements, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class QuestStoreStub : IQuestStore
    {
        public IReadOnlyList<PlayerQuestView> Active { get; init; } = [];
        public long LastSeasonId { get; private set; }
        public (long ChatId, long UserId) LastPlayer { get; private set; }
        public DateTimeOffset LastNow { get; private set; }

        public Task<IReadOnlyList<QuestProgressUpdate>> ApplyGameCompletedAsync(MetaSeason season, long chatId, long userId, GameCompletedMetaEvent ev, CancellationToken ct) => Task.FromResult<IReadOnlyList<QuestProgressUpdate>>([]);

        public Task<IReadOnlyList<PlayerQuestView>> GetQuestsAsync(MetaSeason season, long chatId, long userId, DateTimeOffset now, CancellationToken ct)
        {
            LastSeasonId = season.Id;
            LastPlayer = (chatId, userId);
            LastNow = now;
            return Task.FromResult(Active);
        }

        public Task<QuestClaimResult?> TryMarkClaimedAsync(MetaSeason season, long chatId, long userId, string questId, DateTimeOffset now, CancellationToken ct) => Task.FromResult<QuestClaimResult?>(null);
    }

    private sealed class CapturingEffectExecutor : IAtomicEffectExecutor
    {
        public AtomicEffectExecutionEnvelope? Envelope { get; private set; }
        public IReadOnlyList<IAtomicEffect> Effects { get; private set; } = [];
        public Dictionary<string, object?> Outputs { get; } = new(StringComparer.Ordinal);

        public Task<TResult> ExecuteAsync<TResult>(AtomicEffectExecutionEnvelope envelope, AtomicEffectPlan<TResult> plan, CancellationToken ct)
        {
            Envelope = envelope;
            Effects = plan.Effects;
            var result = plan.ResultFactory is { } factory ? factory(Outputs) : plan.Result;
            return Task.FromResult(result);
        }
    }
}
