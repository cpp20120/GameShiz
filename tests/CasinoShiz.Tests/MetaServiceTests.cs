using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;
using Games.Meta.Application.Effects;
using Games.Meta.Application.Meta;
using Games.Meta.Application.Quests;
using Games.Meta.Application.Risk;
using Games.Meta.Domain.Quests;
using Games.Meta.Domain.Risk;
using Games.Meta.Domain.Seasons;
using Games.Meta.Infrastructure.History;
using Games.Meta.Infrastructure.Persistence;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class MetaServiceTests
{
    [Fact]
    public async Task QuestService_ClaimAsync_AppliesRewardsAndHistory()
    {
        var season = Season();
        var meta = new FakeMetaService(season);
        var quests = new FakeQuestStore
        {
            ClaimResult = new QuestClaimResult("quest-1", "Quest", 125, 250, Claimed: true),
        };
        var economics = new FakeEconomicsService();
        var history = new FakeHistoryStore();
        var service = new QuestService(meta, quests, new QuestAtomicEffectExecutor(meta, quests, economics, history));

        var result = await service.ClaimAsync(100, 42, "Alice", "quest-1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.Claimed);
        Assert.Equal(1, meta.ActiveSeasonCalls);
        Assert.Equal(1, quests.TryMarkClaimedCalls);
        Assert.Equal(1, economics.EnsureUserCalls);
        Assert.Equal(1, economics.CreditCalls);
        Assert.Equal(1, meta.AddSeasonXpCalls);
        Assert.Equal(1, history.AppendCalls);
        Assert.Equal(250, economics.LastCreditAmount);
        Assert.Equal(125, meta.LastXpDelta);
        Assert.Equal("quest.claimed", history.LastEventType);
    }

    [Fact]
    public async Task QuestService_ClaimAsync_WhenNotClaimed_DoesNotMutateRewards()
    {
        var season = Season();
        var meta = new FakeMetaService(season);
        var quests = new FakeQuestStore
        {
            ClaimResult = new QuestClaimResult("quest-1", "Quest", 0, 0, Claimed: false),
        };
        var economics = new FakeEconomicsService();
        var history = new FakeHistoryStore();
        var service = new QuestService(meta, quests, new QuestAtomicEffectExecutor(meta, quests, economics, history));

        var result = await service.ClaimAsync(100, 42, "Alice", "quest-1", CancellationToken.None);

        Assert.NotNull(result);
        Assert.False(result!.Claimed);
        Assert.Equal(0, economics.EnsureUserCalls);
        Assert.Equal(0, economics.CreditCalls);
        Assert.Equal(0, meta.AddSeasonXpCalls);
        Assert.Equal(0, history.AppendCalls);
    }

    [Fact]
    public async Task QuestService_Wrappers_UseActiveSeason()
    {
        var season = Season();
        var meta = new FakeMetaService(season);
        var quests = new FakeQuestStore
        {
            ApplyResult = [new QuestProgressUpdate("quest-1", 2, 5, Completed: false)],
            ActiveQuests = [new PlayerQuestView("quest-1", "Quest", "Desc", "daily", 2, 5, Completed: false, Claimed: false, 10, 20)],
        };
        var service = new QuestService(meta, quests, new QuestAtomicEffectExecutor(meta, quests, new FakeEconomicsService(), new FakeHistoryStore()));
        var ev = new GameCompletedMetaEvent(100, 42, "Alice", MiniGameIds.Dice, 10, 20, IsWin: true, 2, 1);

        var applied = await service.ApplyGameCompletedAsync(ev, CancellationToken.None);
        var active = await service.GetQuestsAsync(100, 42, CancellationToken.None);

        Assert.Single(applied);
        Assert.Single(active);
        Assert.Equal(2, applied[0].Progress);
        Assert.Equal(1, quests.ApplyCalls);
        Assert.Equal(2, meta.ActiveSeasonCalls);
    }

    [Fact]
    public async Task RiskService_EvaluateGameCompletedAsync_RecordsRiskFlags()
    {
        var season = Season();
        var meta = new FakeMetaService(season);
        var risks = new FakeRiskStore();
        var history = new FakeHistoryStore();
        var service = new RiskService(meta, risks, history);
        var player = new SeasonPlayer(1, 100, 42, "Alice", 1_000, 10, 1500, 50, 45, 5, 5_000, 9_000, DateTimeOffset.UtcNow);
        var ev = new GameCompletedMetaEvent(100, 42, "Alice", MiniGameIds.Dice, 10_000, 10_000, IsWin: true, 50, 1);

        await service.EvaluateGameCompletedAsync(ev, player, CancellationToken.None);

        Assert.Equal(3, risks.UpsertCalls);
        Assert.Equal(3, history.AppendCalls);
        Assert.Contains(risks.Kinds, x => string.Equals(x, "large_multiplier", StringComparison.Ordinal));
        Assert.Contains(risks.Kinds, x => string.Equals(x, "large_payout", StringComparison.Ordinal));
        Assert.Contains(risks.Kinds, x => string.Equals(x, "high_win_rate", StringComparison.Ordinal));
        Assert.Contains(risks.Severities, x => string.Equals(x, "high", StringComparison.Ordinal));
        Assert.Contains(risks.Severities, x => string.Equals(x, "critical", StringComparison.Ordinal));
        Assert.Equal("risk.flagged", history.LastEventType);
    }

    [Fact]
    public async Task RiskService_UpdateStatusAsync_OnlyLogsWhenUpdated()
    {
        var season = Season();
        var meta = new FakeMetaService(season);
        var risks = new FakeRiskStore
        {
            UpdateResult = new RiskResolveResult(Updated: true, "ok"),
        };
        var history = new FakeHistoryStore();
        var service = new RiskService(meta, risks, history);

        var result = await service.UpdateStatusAsync(77, "resolved", CancellationToken.None);

        Assert.True(result.Updated);
        Assert.Equal(1, risks.UpdateCalls);
        Assert.Equal(1, history.AppendCalls);
        Assert.Equal("risk.status_updated", history.LastEventType);

        risks.UpdateResult = new RiskResolveResult(Updated: false, "skip");
        var skipped = await service.UpdateStatusAsync(78, "open", CancellationToken.None);

        Assert.False(skipped.Updated);
        Assert.Equal(2, risks.UpdateCalls);
        Assert.Equal(1, history.AppendCalls);
    }

    [Fact]
    public async Task RiskService_GetOpenAsync_UsesActiveSeason()
    {
        var season = Season();
        var meta = new FakeMetaService(season);
        var risks = new FakeRiskStore
        {
            OpenFlags = [new RiskFlagView(1, 100, 42, "Alice", "large_payout", "high", "open", "reason", DateTimeOffset.UtcNow)],
        };
        var service = new RiskService(meta, risks, new FakeHistoryStore());

        var open = await service.GetOpenAsync(100, 5, CancellationToken.None);

        Assert.Single(open);
        Assert.Equal(1, meta.ActiveSeasonCalls);
        Assert.Equal(1, risks.GetOpenCalls);
    }

    private static MetaSeason Season() =>
        new(7, "season", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "active", "{}");

    private sealed class FakeMetaService : IMetaService
    {
        private readonly MetaSeason _season;

        public FakeMetaService(MetaSeason season) => _season = season;

        public int ActiveSeasonCalls { get; private set; }
        public int AddSeasonXpCalls { get; private set; }
        public long LastXpDelta { get; private set; }
        public MetaSeason CurrentSeason => _season;

        public Task<MetaSeason> GetActiveSeasonAsync(CancellationToken ct)
        {
            ActiveSeasonCalls++;
            return Task.FromResult(_season);
        }

        public Task<SeasonProfile> GetProfileAsync(long chatId, long userId, string displayName, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<SeasonLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlayerAchievementView>> GetAchievementsAsync(long chatId, long userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<GameStreakRecordResult?> RecordGamePlayedAsync(long seasonId, long chatId, long userId, string gameKey, DateOnly playedOn, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PlayerGameStreakView>> GetGameStreaksAsync(long chatId, long userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<SeasonPlayer> ApplyGameCompletedAsync(long chatId, long userId, string displayName, long stake, long payout, bool isWin, CancellationToken ct) => throw new NotImplementedException();

        public Task<SeasonPlayer> AddSeasonXpAsync(long seasonId, long chatId, long userId, string displayName, long xpDelta, CancellationToken ct)
        {
            AddSeasonXpCalls++;
            LastXpDelta = xpDelta;
            return Task.FromResult(new SeasonPlayer(seasonId, chatId, userId, displayName, xpDelta, 1, 1, 0, 0, 0, 0, 0, DateTimeOffset.UtcNow));
        }

        public Task<IReadOnlyList<AchievementUnlock>> UnlockAchievementsAsync(long seasonId, long chatId, long userId, IEnumerable<AchievementDefinition> achievements, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeQuestStore : IQuestStore
    {
        public QuestClaimResult? ClaimResult { get; set; }
        public IReadOnlyList<QuestProgressUpdate> ApplyResult { get; set; } = [];
        public IReadOnlyList<PlayerQuestView> ActiveQuests { get; set; } = [];

        public int ApplyCalls { get; private set; }
        public int TryMarkClaimedCalls { get; private set; }
        public int GetQuestsCalls { get; private set; }

        public Task<IReadOnlyList<QuestProgressUpdate>> ApplyGameCompletedAsync(MetaSeason season, long chatId, long userId, GameCompletedMetaEvent ev, CancellationToken ct)
        {
            ApplyCalls++;
            return Task.FromResult(ApplyResult);
        }

        public Task<IReadOnlyList<PlayerQuestView>> GetQuestsAsync(MetaSeason season, long chatId, long userId, DateTimeOffset now, CancellationToken ct)
        {
            GetQuestsCalls++;
            return Task.FromResult(ActiveQuests);
        }

        public Task<QuestClaimResult?> TryMarkClaimedAsync(MetaSeason season, long chatId, long userId, string questId, DateTimeOffset now, CancellationToken ct)
        {
            TryMarkClaimedCalls++;
            return Task.FromResult(ClaimResult);
        }
    }

    private sealed class QuestAtomicEffectExecutor(
        FakeMetaService meta,
        FakeQuestStore quests,
        FakeEconomicsService economics,
        FakeHistoryStore history) : IAtomicEffectExecutor
    {
        public async Task<TResult> ExecuteAsync<TResult>(
            AtomicEffectExecutionEnvelope envelope,
            AtomicEffectPlan<TResult> plan,
            CancellationToken ct)
        {
            var outputs = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var effect in plan.Effects)
            {
                switch (effect)
                {
                    case QuestProgressAtomicEffect progress:
                        outputs["updates"] = await quests.ApplyGameCompletedAsync(meta.CurrentSeason, progress.ChatId, progress.UserId, progress.Completion, ct);
                        break;
                    case QuestClaimAtomicEffect claim:
                    {
                        var result = await quests.TryMarkClaimedAsync(
                            meta.CurrentSeason,
                            claim.ChatId,
                            claim.UserId,
                            claim.QuestId,
                            claim.Now,
                            ct);
                        if (result is { Claimed: true })
                        {
                            await economics.EnsureUserAsync(claim.UserId, claim.ChatId, claim.DisplayName, ct);
                            if (result.RewardCoins > 0)
                                await economics.CreditAsync(claim.UserId, claim.ChatId, checked((int)result.RewardCoins), "quest.reward", ct);
                            if (result.RewardXp > 0)
                                await meta.AddSeasonXpAsync(claim.SeasonId, claim.ChatId, claim.UserId, claim.DisplayName, result.RewardXp, ct);
                            await history.AppendAsync("quest.claimed", "player", $"{claim.SeasonId}:{claim.ChatId}:{claim.UserId}", claim.SeasonId, claim.ChatId, claim.UserId, result, ct);
                        }
                        outputs["result"] = result;
                        break;
                    }
                    default:
                        throw new InvalidOperationException($"Unexpected test effect: {effect.GetType().Name}");
                }
            }

            return plan.ResultFactory is { } factory ? factory(outputs) : plan.Result;
        }
    }

    private sealed class FakeEconomicsService : IEconomicsService
    {
        public int EnsureUserCalls { get; private set; }
        public int CreditCalls { get; private set; }
        public long LastCreditAmount { get; private set; }

        public Task EnsureUserAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct)
        {
            EnsureUserCalls++;
            return Task.CompletedTask;
        }

        public Task<int> GetBalanceAsync(long userId, long balanceScopeId, CancellationToken ct) => Task.FromResult(0);
        public Task<bool> TryDebitAsync(long userId, long balanceScopeId, int amount, string reason, CancellationToken ct) => Task.FromResult(false);
        public Task DebitAsync(long userId, long balanceScopeId, int amount, string reason, CancellationToken ct) => Task.CompletedTask;

        public Task CreditAsync(long userId, long balanceScopeId, int amount, string reason, CancellationToken ct)
        {
            CreditCalls++;
            LastCreditAmount = amount;
            return Task.CompletedTask;
        }

        public Task AdjustUncheckedAsync(long userId, long balanceScopeId, int delta, CancellationToken ct) => Task.CompletedTask;
        public Task<LedgerRevertResult> RevertLedgerEntryAsync(long economicsLedgerId, CancellationToken ct) => Task.FromResult(new LedgerRevertResult(LedgerRevertStatus.NoEffect, 0));
        public Task<PeerTransferResult> TryPeerTransferAsync(long fromUserId, long toUserId, long balanceScopeId, int debitFromSender, int creditToRecipient, string senderReason, string recipientReason, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeHistoryStore : IMetaHistoryStore
    {
        public int AppendCalls { get; private set; }
        public string? LastEventType { get; private set; }

        public Task AppendAsync(string eventType, string aggregateType, string aggregateId, long? seasonId, long? chatId, long? userId, object payload, CancellationToken ct)
        {
            AppendCalls++;
            LastEventType = eventType;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MetaHistoryEvent>> ListAsync(string? eventType, string? aggregateType, string? aggregateId, long? chatId, long? userId, int limit, CancellationToken ct) => throw new NotImplementedException();
        public Task<MetaHistoryStats> GetStatsAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeRiskStore : IRiskStore
    {
        public int UpsertCalls { get; private set; }
        public int GetOpenCalls { get; private set; }
        public int UpdateCalls { get; private set; }
        public List<string> Kinds { get; } = [];
        public List<string> Severities { get; } = [];
        public IReadOnlyList<RiskFlagView> OpenFlags { get; set; } = [];
        public RiskResolveResult UpdateResult { get; set; } = new(Updated: false, "no-op");

        public Task UpsertOpenAsync(MetaSeason season, long chatId, long userId, string displayName, string kind, string severity, string reason, string evidenceJson, CancellationToken ct)
        {
            UpsertCalls++;
            Kinds.Add(kind);
            Severities.Add(severity);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RiskFlagView>> GetOpenAsync(MetaSeason season, long chatId, int limit, CancellationToken ct)
        {
            GetOpenCalls++;
            return Task.FromResult(OpenFlags);
        }

        public Task<RiskResolveResult> UpdateStatusAsync(long flagId, string status, CancellationToken ct)
        {
            UpdateCalls++;
            return Task.FromResult(UpdateResult);
        }
    }
}
