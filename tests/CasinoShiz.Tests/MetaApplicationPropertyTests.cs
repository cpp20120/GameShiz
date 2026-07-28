using BotFramework.Host.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Workflows;
using BotFramework.Sdk.Events.Bus;
using BotFramework.Sdk.Events.Meta;
using BotFramework.Sdk.Execution;
using BotFramework.Sdk.MiniGames;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Effects;
using Games.Meta.Application.Meta;
using Games.Meta.Application.Quests;
using Games.Meta.Application.Risk;
using Games.Meta.Application.Seasons;
using Games.Meta.Application.Tournaments;
using Games.Meta.Domain.Achievements;
using Games.Meta.Domain.Clans;
using Games.Meta.Domain.Quests;
using Games.Meta.Domain.Risk;
using Games.Meta.Domain.Seasons;
using Games.Meta.Domain.Streaks;
using Games.Meta.Domain.Tournaments;
using Games.Meta.Infrastructure.History;
using Games.Meta.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class MetaApplicationPropertyTests
{
    [Property(MaxTest = 100)]
    public async Task<Property> SeasonRewardService_UsesStableSeasonEnvelope(PositiveInt rawSeason)
    {
        var seasonId = 1L + rawSeason.Get % 100_000;
        var effects = new CapturingEffectExecutor();
        var service = new SeasonRewardService(effects);

        await service.ProcessPlayerRewardsAsync(seasonId, CancellationToken.None);
        var playerCall = effects.Calls[^1];
        await service.ProcessClanRewardsAsync(seasonId, CancellationToken.None);
        var clanCall = effects.Calls[^1];

        var playerEffect = playerCall.Effects.Single() is SeasonPlayerRewardsAtomicEffect player
            && player.SeasonId == seasonId;
        var clanEffect = clanCall.Effects.Single() is SeasonClanRewardsAtomicEffect clan
            && clan.SeasonId == seasonId;
        var valid = playerEffect
            && clanEffect
            && playerCall.Envelope.GameId == "meta.season"
            && clanCall.Envelope.GameId == "meta.season"
            && playerCall.Envelope.CommandId == $"meta:season:player-rewards:{seasonId}"
            && clanCall.Envelope.CommandId == $"meta:season:clan-rewards:{seasonId}"
            && playerCall.Envelope.AggregateId == $"season:{seasonId}"
            && clanCall.Envelope.AggregateId == $"season:{seasonId}"
            && playerCall.Envelope.LockKeys.SequenceEqual([$"game:meta.season:{seasonId}"])
            && clanCall.Envelope.LockKeys.SequenceEqual([$"game:meta.season:{seasonId}"]);

        return valid
            .ToProperty()
            .Label($"season={seasonId}, calls={effects.Calls.Count}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> RiskService_EmitsExactlyTheTriggeredFlags(
        NonNegativeInt rawMultiplier,
        NonNegativeInt rawPayout,
        NonNegativeInt rawGames,
        NonNegativeInt rawWins)
    {
        var multiplier = rawMultiplier.Get % 70;
        var payout = rawPayout.Get;
        var games = rawGames.Get % 100;
        var wins = games == 0 ? 0 : rawWins.Get % (games + 1);
        var risks = new CapturingRiskStore();
        var history = new CapturingHistoryStore();
        var service = new RiskService(new ApplicationMetaStub(Season()), risks, history);
        var player = Player(games, wins, totalStaked: 1_000, totalPayout: payout);
        var ev = new GameCompletedMetaEvent(
            100,
            42,
            "Alice",
            MiniGameIds.Dice,
            100,
            payout,
            payout > 0,
            multiplier,
            DateTimeOffset.UnixEpoch.ToUnixTimeMilliseconds());

        await service.EvaluateGameCompletedAsync(ev, player, CancellationToken.None);

        var largeMultiplier = multiplier >= 20 && payout > 0;
        var largePayout = payout >= 1_000;
        var highWinRate = games >= 20 && wins * 100.0 / Math.Max(1, games) >= 85.0;
        var expected = (largeMultiplier ? 1 : 0) + (largePayout ? 1 : 0) + (highWinRate ? 1 : 0);
        var valid = risks.Upserts.Count == expected
            && history.Entries.Count == expected
            && (!largeMultiplier || risks.Kinds.Contains("large_multiplier", StringComparer.Ordinal))
            && (!largePayout || risks.Kinds.Contains("large_payout", StringComparer.Ordinal))
            && (!highWinRate || risks.Kinds.Contains("high_win_rate", StringComparer.Ordinal))
            && risks.Upserts.All(x => x.Severity is "medium" or "high" or "critical");

        return valid
            .ToProperty()
            .Label($"multiplier={multiplier}, payout={payout}, games={games}, wins={wins}, flags={risks.Upserts.Count}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> QuestProjection_LogsExactlyCompletedUpdates(NonNegativeInt rawSeed)
    {
        var count = rawSeed.Get % 10;
        var updates = Enumerable.Range(0, count)
            .Select(index => new QuestProgressUpdate(
                $"quest-{index}",
                index + 1,
                index + 2,
                (rawSeed.Get + index) % 2 == 0))
            .ToArray();
        var history = new CapturingHistoryStore();
        var projection = new QuestProjection(
            new CapturingQuestService { Updates = updates },
            new ApplicationMetaStub(Season()),
            history,
            NullLogger<QuestProjection>.Instance);

        await ((IDomainEventSubscriber)projection).HandleAsync(Event(rawSeed.Get), CancellationToken.None);

        var expected = updates.Count(x => x.Completed);
        return (history.Entries.Count == expected
                && history.Entries.All(x => x.EventType == "quest.progressed")
                && history.Entries.Select(x => x.AggregateId).Distinct(StringComparer.Ordinal).Count() <= 1)
            .ToProperty()
            .Label($"updates={updates.Length}, completed={expected}, logged={history.Entries.Count}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> ClanProjection_ForwardsEventWithoutMutation(NonNegativeInt rawSeed)
    {
        var clans = new CapturingClanService();
        var projection = new ClanProjection(clans, NullLogger<ClanProjection>.Instance);
        var ev = Event(rawSeed.Get);

        await ((IDomainEventSubscriber)projection).HandleAsync(ev, CancellationToken.None);

        return (clans.Applied == ev && clans.ApplyCalls == 1)
            .ToProperty()
            .Label($"chat={ev.ChatId}, user={ev.UserId}, game={ev.GameKey}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> TournamentCommandExecutor_LegacyCommandsKeepIdentity(
        PositiveInt rawTournament,
        PositiveInt rawMatch,
        NonNegativeInt rawChat,
        NonNegativeInt rawUser)
    {
        var tournamentId = 1L + rawTournament.Get % 100_000;
        var matchId = 1L + rawMatch.Get % 100_000;
        var chatId = rawChat.Get;
        var userId = rawUser.Get;
        var victorId = userId + 1;
        var effects = new CapturingEffectExecutor();
        var store = new CapturingTournamentStore
        {
            Tournament = new TournamentInfo(
                tournamentId,
                7,
                chatId,
                MiniGameIds.Dice,
                "single_elim",
                "open",
                100,
                8,
                userId,
                DateTimeOffset.UnixEpoch,
                2,
                500),
            Match = new TournamentMatchInfo(
                matchId,
                tournamentId,
                1,
                1,
                "ready",
                userId,
                "Alice",
                victorId,
                "Bob",
                null,
                DateTimeOffset.UnixEpoch,
                DateTimeOffset.UnixEpoch),
            Players = [new TournamentPlayerInfo(tournamentId, userId, "Alice", "joined", DateTimeOffset.UnixEpoch)],
        };
        var executor = new TournamentCommandExecutor(
            new ApplicationMetaStub(Season()),
            store,
            effects);

        await executor.CreateAsync(chatId, userId, "dice", 10, 8, "create", CancellationToken.None);
        await executor.JoinAsync(tournamentId, userId, chatId, "Alice", "join", CancellationToken.None);
        await executor.StartAsync(tournamentId, userId, "start", CancellationToken.None);
        await executor.ReportMatchAsync(matchId, userId, victorId, "report", CancellationToken.None);
        await executor.FinishAsync(tournamentId, userId, victorId, "finish", CancellationToken.None);
        await executor.CancelAsync(tournamentId, userId, "cancel", CancellationToken.None);

        var create = effects.Calls[0];
        var join = effects.Calls[1];
        var start = effects.Calls[2];
        var report = effects.Calls[3];
        var finish = effects.Calls[4];
        var cancel = effects.Calls[5];
        var valid = effects.Calls.Count == 6
            && create.Envelope.AggregateId == $"7:{chatId}"
            && create.Envelope.CommandId == "create"
            && create.Effects.Single() is TournamentCreateAtomicEffect
            && join.Envelope.AggregateId == tournamentId.ToString(System.Globalization.CultureInfo.InvariantCulture)
            && join.Envelope.LockKeys.Contains($"wallet:{chatId}:{userId}", StringComparer.Ordinal)
            && join.Effects.Single() is TournamentJoinAtomicEffect
            && start.Envelope.LockKeys.SequenceEqual([$"game:meta.tournament:{tournamentId}"])
            && start.Effects.Single() is TournamentStartAtomicEffect
            && report.Envelope.AggregateId == $"match:{matchId}"
            && report.Envelope.LockKeys.Contains($"wallet:{chatId}:{victorId}", StringComparer.Ordinal)
            && report.Effects.Single() is TournamentReportAtomicEffect
            && finish.Envelope.LockKeys.Contains($"wallet:{chatId}:{victorId}", StringComparer.Ordinal)
            && finish.Effects.Single() is TournamentFinishAtomicEffect
            && cancel.Envelope.LockKeys.Contains($"wallet:{chatId}:{userId}", StringComparer.Ordinal)
            && cancel.Effects.Single() is TournamentCancelAtomicEffect;

        return valid
            .ToProperty()
            .Label($"tournament={tournamentId}, match={matchId}, calls={effects.Calls.Count}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> TournamentWalletWorkflow_CompensatesRejectedLocalTransitions(
        PositiveInt rawTournament,
        PositiveInt rawMatch,
        PositiveInt rawFee,
        PositiveInt rawPrize)
    {
        var tournamentId = 1L + rawTournament.Get % 100_000;
        var matchId = 1L + rawMatch.Get % 100_000;
        var chatId = 100L + rawTournament.Get % 100_000;
        var ownerId = 200L + rawTournament.Get % 100_000;
        var participantId = ownerId + 1;
        var opponentId = ownerId + 2;
        var entryFee = 1 + rawFee.Get % 10_000;
        var prizePool = 1 + rawPrize.Get % 10_000;
        var wallet = new CapturingWallet();
        var effects = new CapturingEffectExecutor();

        var join = new TournamentCommandExecutor(
            new ApplicationMetaStub(Season()),
            new CapturingTournamentStore
            {
                Tournament = new TournamentInfo(
                    tournamentId,
                    7,
                    chatId,
                    MiniGameIds.Dice,
                    "single_elim",
                    "open",
                    entryFee,
                    8,
                    ownerId,
                    DateTimeOffset.UnixEpoch,
                    0,
                    prizePool),
                Players = [],
            },
            effects,
            wallet);

        var joinResult = await join.JoinAsync(
            tournamentId,
            participantId,
            chatId,
            "Alice",
            "join-command",
            CancellationToken.None);

        var match = new TournamentMatchInfo(
            matchId,
            tournamentId,
            1,
            1,
            "ready",
            participantId,
            "Alice",
            opponentId,
            "Bob",
            null,
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch);
        var startedTournament = new TournamentInfo(
            tournamentId,
            7,
            chatId,
            MiniGameIds.Dice,
            "single_elim",
            "started",
            entryFee,
            8,
            ownerId,
            DateTimeOffset.UnixEpoch,
            2,
            prizePool);
        var report = new TournamentCommandExecutor(
            new ApplicationMetaStub(Season()),
            new CapturingTournamentStore
            {
                Tournament = startedTournament,
                Match = match,
                Players =
                [
                    new TournamentPlayerInfo(tournamentId, participantId, "Alice", "joined", DateTimeOffset.UnixEpoch),
                    new TournamentPlayerInfo(tournamentId, opponentId, "Bob", "joined", DateTimeOffset.UnixEpoch),
                ],
            },
            effects,
            wallet);

        var reportResult = await report.ReportMatchAsync(
            matchId,
            ownerId,
            participantId,
            "report-command",
            CancellationToken.None);

        var finish = new TournamentCommandExecutor(
            new ApplicationMetaStub(Season()),
            new CapturingTournamentStore
            {
                Tournament = startedTournament,
                Players = [new TournamentPlayerInfo(tournamentId, participantId, "Alice", "joined", DateTimeOffset.UnixEpoch)],
            },
            effects,
            wallet);
        var finishResult = await finish.FinishAsync(
            tournamentId,
            ownerId,
            participantId,
            "finish-command",
            CancellationToken.None);

        var cancel = new TournamentCommandExecutor(
            new ApplicationMetaStub(Season()),
            new CapturingTournamentStore
            {
                Tournament = startedTournament,
                Players =
                [
                    new TournamentPlayerInfo(tournamentId, participantId, "Alice", "joined", DateTimeOffset.UnixEpoch),
                    new TournamentPlayerInfo(tournamentId, opponentId, "Bob", "joined", DateTimeOffset.UnixEpoch),
                ],
            },
            effects,
            wallet);
        var cancelResult = await cancel.CancelAsync(tournamentId, ownerId, "cancel-command", CancellationToken.None);

        var zeroNetPerUser = wallet.Mutations
            .GroupBy(static mutation => mutation.UserId)
            .All(group => group.Sum(mutation => mutation.Effect.Kind == WalletBatchEffectKind.Credit
                ? mutation.Effect.Amount
                : -mutation.Effect.Amount) == 0);
        var valid = !joinResult.Joined
            && !reportResult.Updated
            && !reportResult.Finished
            && finishResult is null
            && cancelResult is null
            && effects.Calls.Count == 4
            && effects.Calls[0].Effects.Single() is TournamentJoinAtomicEffect { WalletAlreadyApplied: true }
            && effects.Calls[1].Effects.Single() is TournamentReportAtomicEffect { PrizeAlreadyPaid: true }
            && effects.Calls[2].Effects.Single() is TournamentFinishAtomicEffect { PrizeAlreadyPaid: true }
            && effects.Calls[3].Effects.Single() is TournamentCancelAtomicEffect { RefundsAlreadyPaid: true }
            && wallet.Mutations.Count == 10
            && wallet.Mutations.Count(mutation => mutation.Effect.Kind == WalletBatchEffectKind.Debit) == 5
            && wallet.Mutations.Count(mutation => mutation.Effect.Kind == WalletBatchEffectKind.Credit) == 5
            && zeroNetPerUser
            && wallet.Mutations.Any(mutation => mutation.Effect.Reason == "tournament.entry_fee.rollback")
            && wallet.Mutations.Any(mutation => mutation.Effect.Reason == "tournament.prize.rollback")
            && wallet.Mutations.Any(mutation => mutation.Effect.Reason == "tournament.cancel.refund.rollback");

        return valid
            .ToProperty()
            .Label($"tournament={tournamentId}, fee={entryFee}, prize={prizePool}, walletMutations={wallet.Mutations.Count}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> TournamentWorkflowHandler_PreservesOperationIdentity(PositiveInt rawId)
    {
        var id = 1L + rawId.Get % 100_000;
        var steps = new CapturingWorkflowStepExecutor();
        var executor = new TournamentCommandExecutor(
            new ApplicationMetaStub(Season()),
            new CapturingTournamentStore(),
            new CapturingEffectExecutor());

        var commands = new IDurableWorkflowCommand[]
        {
            new TournamentCreateWorkflowCommand("create-command", $"workflow:create:{id}", id, id + 1, MiniGameIds.Dice, 10, 8),
            new TournamentJoinWorkflowCommand("join-command", $"workflow:join:{id}", id, id + 1, id + 2, "Alice"),
            new TournamentStartWorkflowCommand("start-command", $"workflow:start:{id}", id, id + 1),
            new TournamentReportWorkflowCommand("report-command", $"workflow:report:{id}", id + 3, id + 1, id + 2),
            new TournamentFinishWorkflowCommand("finish-command", $"workflow:finish:{id}", id, id + 1, id + 2),
            new TournamentCancelWorkflowCommand("cancel-command", $"workflow:cancel:{id}", id, id + 1),
        };

        await TournamentWorkflowHandler.Handle((TournamentCreateWorkflowCommand)commands[0], executor, steps, CancellationToken.None);
        await TournamentJoinWorkflowHandler.Handle((TournamentJoinWorkflowCommand)commands[1], executor, steps, CancellationToken.None);
        await TournamentStartWorkflowHandler.Handle((TournamentStartWorkflowCommand)commands[2], executor, steps, CancellationToken.None);
        await TournamentReportWorkflowHandler.Handle((TournamentReportWorkflowCommand)commands[3], executor, steps, CancellationToken.None);
        await TournamentFinishWorkflowHandler.Handle((TournamentFinishWorkflowCommand)commands[4], executor, steps, CancellationToken.None);
        await TournamentCancelWorkflowHandler.Handle((TournamentCancelWorkflowCommand)commands[5], executor, steps, CancellationToken.None);

        var expectedOperations = new[] { "create", "join", "start", "report", "finish", "cancel" };
        var expectedCommandIds = new[] { "create-command", "join-command", "start-command", "report-command", "finish-command", "cancel-command" };
        var expectedWorkflowIds = new[]
        {
            $"workflow:create:{id}",
            $"workflow:join:{id}",
            $"workflow:start:{id}",
            $"workflow:report:{id}",
            $"workflow:finish:{id}",
            $"workflow:cancel:{id}",
        };
        var expectedAggregates = new string?[] { null, id.ToString(), id.ToString(), (id + 3).ToString(), id.ToString(), id.ToString() };
        var valid = steps.Calls.Count == commands.Length
            && steps.Calls.Select(static call => call.Options.Operation).SequenceEqual(expectedOperations)
            && steps.Calls.Select(static call => call.Options.CommandId).SequenceEqual(expectedCommandIds)
            && steps.Calls.Select(static call => call.Options.WorkflowId).SequenceEqual(expectedWorkflowIds)
            && steps.Calls.Select(static call => call.Options.AggregateId).SequenceEqual(expectedAggregates);

        return valid
            .ToProperty()
            .Label($"id={id}, workflowSteps={steps.Calls.Count}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> MetaXpProjection_AlwaysRecordsGameCompletion(NonNegativeInt rawSeed)
    {
        var history = new CapturingHistoryStore();
        var meta = new ProjectionMetaStub();
        var projection = new MetaXpProjection(
            meta,
            new NoOpRiskService(),
            history,
            new FakeRuntimeTuning(),
            new NullEventBus(),
            NullLogger<MetaXpProjection>.Instance);

        await ((IDomainEventSubscriber)projection).HandleAsync(Event(rawSeed.Get, gameKey: "unknown"), CancellationToken.None);

        return (history.Entries.Count == 1
                && history.Entries[0].EventType == "game.completed"
                && history.Entries[0].AggregateId == "7:100:42"
                && meta.ApplyCalls == 1
                && meta.RecordCalls == 1
                && meta.UnlockCalls == 1)
            .ToProperty()
            .Label($"seed={rawSeed.Get}, history={history.Entries.Count}");
    }

    private static MetaSeason Season() =>
        new(7, "Season 7", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "active", "{}");

    private static SeasonPlayer Player(int games, int wins, long totalStaked, long totalPayout) =>
        new(7, 100, 42, "Alice", 100, 2, 1_000, games, wins, Math.Max(0, games - wins), totalStaked, totalPayout, DateTimeOffset.UnixEpoch);

    private static GameCompletedMetaEvent Event(int seed, string gameKey = MiniGameIds.Dice) =>
        new(100, 42, "Alice", gameKey, seed % 10_000, seed % 10_000, seed % 2 == 0, 1 + seed % 60, seed % 1_000_000);

    private sealed class CapturingEffectExecutor : IAtomicEffectExecutor
    {
        public List<EffectCall> Calls { get; } = [];

        public Task<TResult> ExecuteAsync<TResult>(AtomicEffectExecutionEnvelope envelope, AtomicEffectPlan<TResult> plan, CancellationToken ct)
        {
            Calls.Add(new EffectCall(envelope, plan.Effects));
            var outputs = new Dictionary<string, object?>(StringComparer.Ordinal) { ["result"] = plan.Result };
            var result = plan.ResultFactory is { } factory ? factory(outputs) : plan.Result;
            return Task.FromResult(result);
        }
    }

    private sealed record EffectCall(AtomicEffectExecutionEnvelope Envelope, IReadOnlyList<IAtomicEffect> Effects);

    private sealed class CapturingWorkflowStepExecutor : IDurableWorkflowStepExecutor
    {
        public List<WorkflowCall> Calls { get; } = [];

        public async Task<TResult> ExecuteAsync<TResult>(
            object command,
            DurableWorkflowExecutionOptions options,
            Func<Task<TResult>> execute,
            Func<TResult, bool> succeeded,
            Func<TResult, bool> terminal,
            Func<TResult, string?> aggregateId,
            Func<TResult, object> payload,
            CancellationToken ct)
        {
            Calls.Add(new WorkflowCall(command, options));
            var result = await execute();
            _ = succeeded(result);
            _ = terminal(result);
            _ = aggregateId(result);
            _ = payload(result);
            return result;
        }
    }

    private sealed record WorkflowCall(object Command, DurableWorkflowExecutionOptions Options);

    private class ApplicationMetaStub(MetaSeason season) : IMetaService
    {
        public ApplicationMetaStub() : this(Season()) { }

        public virtual Task<MetaSeason> GetActiveSeasonAsync(CancellationToken ct) => Task.FromResult(season);
        public virtual Task<SeasonProfile> GetProfileAsync(long chatId, long userId, string displayName, CancellationToken ct) => throw new NotSupportedException();
        public virtual Task<IReadOnlyList<SeasonLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct) => throw new NotSupportedException();
        public virtual Task<IReadOnlyList<PlayerAchievementView>> GetAchievementsAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public virtual Task<GameStreakRecordResult?> RecordGamePlayedAsync(long seasonId, long chatId, long userId, string gameKey, DateOnly playedOn, CancellationToken ct) => Task.FromResult<GameStreakRecordResult?>(null);
        public virtual Task<IReadOnlyList<PlayerGameStreakView>> GetGameStreaksAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public virtual Task<SeasonPlayer> ApplyGameCompletedAsync(long chatId, long userId, string displayName, long stake, long payout, bool isWin, CancellationToken ct) => throw new NotSupportedException();
        public virtual Task<SeasonPlayer> AddSeasonXpAsync(long seasonId, long chatId, long userId, string displayName, long xpDelta, CancellationToken ct) => throw new NotSupportedException();
        public virtual Task<IReadOnlyList<AchievementUnlock>> UnlockAchievementsAsync(long seasonId, long chatId, long userId, IEnumerable<AchievementDefinition> achievements, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class ProjectionMetaStub : ApplicationMetaStub
    {
        public int ApplyCalls { get; private set; }
        public int RecordCalls { get; private set; }
        public int UnlockCalls { get; private set; }

        public override Task<SeasonPlayer> ApplyGameCompletedAsync(long chatId, long userId, string displayName, long stake, long payout, bool isWin, CancellationToken ct)
        {
            ApplyCalls++;
            return Task.FromResult(Player(0, 0, 0, 0));
        }

        public override Task<GameStreakRecordResult?> RecordGamePlayedAsync(long seasonId, long chatId, long userId, string gameKey, DateOnly playedOn, CancellationToken ct)
        {
            RecordCalls++;
            return Task.FromResult<GameStreakRecordResult?>(null);
        }

        public override Task<IReadOnlyList<AchievementUnlock>> UnlockAchievementsAsync(long seasonId, long chatId, long userId, IEnumerable<AchievementDefinition> achievements, CancellationToken ct)
        {
            UnlockCalls++;
            return Task.FromResult<IReadOnlyList<AchievementUnlock>>([]);
        }
    }

    private sealed class CapturingRiskStore : IRiskStore
    {
        public List<(string Kind, string Severity)> Upserts { get; } = [];
        public IEnumerable<string> Kinds => Upserts.Select(x => x.Kind);

        public Task UpsertOpenAsync(MetaSeason season, long chatId, long userId, string displayName, string kind, string severity, string reason, string evidenceJson, CancellationToken ct)
        {
            Upserts.Add((kind, severity));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RiskFlagView>> GetOpenAsync(MetaSeason season, long chatId, int limit, CancellationToken ct) => Task.FromResult<IReadOnlyList<RiskFlagView>>([]);
        public Task<RiskResolveResult> UpdateStatusAsync(long flagId, string status, CancellationToken ct) => Task.FromResult(new RiskResolveResult(false, "not configured"));
    }

    private sealed class CapturingHistoryStore : IMetaHistoryStore
    {
        public List<HistoryEntry> Entries { get; } = [];

        public Task AppendAsync(string eventType, string aggregateType, string aggregateId, long? seasonId, long? chatId, long? userId, object payload, CancellationToken ct)
        {
            Entries.Add(new HistoryEntry(eventType, aggregateType, aggregateId));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<MetaHistoryEvent>> ListAsync(string? eventType, string? aggregateType, string? aggregateId, long? chatId, long? userId, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<MetaHistoryStats> GetStatsAsync(CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed record HistoryEntry(string EventType, string AggregateType, string AggregateId);

    private sealed class CapturingQuestService : IQuestService
    {
        public IReadOnlyList<QuestProgressUpdate> Updates { get; init; } = [];
        public Task<IReadOnlyList<QuestProgressUpdate>> ApplyGameCompletedAsync(GameCompletedMetaEvent ev, CancellationToken ct) => Task.FromResult(Updates);
        public Task<IReadOnlyList<PlayerQuestView>> GetQuestsAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<QuestClaimResult?> ClaimAsync(long chatId, long userId, string displayName, string questId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CapturingClanService : IClanService
    {
        public GameCompletedMetaEvent? Applied { get; private set; }
        public int ApplyCalls { get; private set; }
        public Task ApplyGameCompletedAsync(GameCompletedMetaEvent ev, CancellationToken ct)
        {
            Applied = ev;
            ApplyCalls++;
            return Task.CompletedTask;
        }

        public Task<ClanCreateResult> CreateAsync(long chatId, long userId, string displayName, string tag, string name, CancellationToken ct) => throw new NotSupportedException();
        public Task<ClanJoinResult> JoinAsync(long chatId, long userId, string displayName, string tag, CancellationToken ct) => throw new NotSupportedException();
        public Task<ClanInfo?> GetUserClanAsync(long chatId, long userId, CancellationToken ct) => throw new NotSupportedException();
        public Task<ClanInfo?> GetClanByTagAsync(long chatId, string tag, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ClanMemberInfo>> GetMembersAsync(long clanId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ClanLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CapturingTournamentStore : ITournamentStore
    {
        public TournamentInfo? Tournament { get; init; }
        public TournamentMatchInfo? Match { get; init; }
        public IReadOnlyList<TournamentPlayerInfo> Players { get; init; } = [];

        public Task<TournamentCreateResult> CreateAsync(MetaSeason season, long chatId, long createdBy, string gameKey, int entryFee, int maxPlayers, CancellationToken ct) => Task.FromResult(new TournamentCreateResult(false, "not configured"));
        public Task<TournamentJoinResult> JoinAsync(long tournamentId, long userId, string displayName, CancellationToken ct) => Task.FromResult(new TournamentJoinResult(false, "not configured"));
        public Task<TournamentInfo?> GetAsync(long tournamentId, CancellationToken ct) => Task.FromResult(Tournament);
        public Task<IReadOnlyList<TournamentInfo>> GetOpenAsync(MetaSeason season, long chatId, int limit, CancellationToken ct) => Task.FromResult<IReadOnlyList<TournamentInfo>>([]);
        public Task<IReadOnlyList<TournamentPlayerInfo>> GetPlayersAsync(long tournamentId, CancellationToken ct) => Task.FromResult(Players);
        public Task<IReadOnlyList<TournamentMatchInfo>> GetMatchesAsync(long tournamentId, CancellationToken ct) => Task.FromResult<IReadOnlyList<TournamentMatchInfo>>(Match is null ? [] : [Match]);
        public Task<TournamentMatchInfo?> GetMatchAsync(long matchId, CancellationToken ct) => Task.FromResult(Match);
        public Task<bool> StartAsync(long tournamentId, long userId, CancellationToken ct) => Task.FromResult(false);
        public Task<TournamentReportResult> ReportMatchAsync(long matchId, long actorUserId, long victorUserId, CancellationToken ct) => Task.FromResult(new TournamentReportResult(false, false, "not configured"));
        public Task<TournamentPlayerInfo?> FinishAsync(long tournamentId, long actorUserId, long winnerUserId, CancellationToken ct) => Task.FromResult<TournamentPlayerInfo?>(null);
        public Task<IReadOnlyList<TournamentPlayerInfo>?> CancelAsync(long tournamentId, long actorUserId, CancellationToken ct) => Task.FromResult<IReadOnlyList<TournamentPlayerInfo>?>(null);
    }

    private sealed class NoOpRiskService : IRiskService
    {
        public Task EvaluateGameCompletedAsync(GameCompletedMetaEvent ev, SeasonPlayer player, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<RiskFlagView>> GetOpenAsync(long chatId, int limit, CancellationToken ct) => throw new NotSupportedException();
        public Task<RiskResolveResult> UpdateStatusAsync(long flagId, string status, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class CapturingWallet : IWalletAtomicExecutionService
    {
        public List<WalletMutation> Mutations { get; } = [];

        public Task EnsureUserAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct) => Task.CompletedTask;

        public Task<int> GetBalanceAsync(long userId, long balanceScopeId, CancellationToken ct) => Task.FromResult(0);

        public Task<WalletBatchMutationResult> ApplyBatchAsync(
            long userId,
            long balanceScopeId,
            IReadOnlyList<WalletBatchEffect> effects,
            string operationId,
            CancellationToken ct)
        {
            Mutations.Add(new WalletMutation(userId, balanceScopeId, effects.Single(), operationId));
            return Task.FromResult(new WalletBatchMutationResult(Applied: true, Rejected: false, NewBalance: 0));
        }
    }

    private sealed record WalletMutation(
        long UserId,
        long BalanceScopeId,
        WalletBatchEffect Effect,
        string OperationId);
}
