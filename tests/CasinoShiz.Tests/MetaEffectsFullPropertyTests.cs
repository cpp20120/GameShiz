using System.Globalization;
using BotFramework.Host.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Host.Execution;
using BotFramework.Sdk.Admin.Execution;
using BotFramework.Sdk.Events.Meta;
using BotFramework.Sdk.Execution;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Games.Meta.Application.Clans;
using Games.Meta.Application.Effects;
using Games.Meta.Application.Models;
using Games.Meta.Domain.Quests;
using Games.Meta.Domain.Seasons;
using Games.Meta.Domain.Tournaments;
using Games.Meta.Infrastructure.Catalog;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class MetaEffectsFullPropertyTests
{
    [Property(MaxTest = 100)]
    public async Task<Property> SeasonRewardEffects_PayOnlyPositiveConfiguredPlaces(PositiveInt raw)
    {
        var seasonId = raw.Get;
        var amount = 1 + raw.Get % 10_000;
        var config = RewardsJson(amount);
        var playerContext = new AtomicScript { Wallet = new RecordingWallet() };
        playerContext.EnqueueSingle<string>(config);
        playerContext.EnqueueList(
            new PlayerSeasonRewardWinner(1, 10, 20, "Alice", 100, 1_000),
            new PlayerSeasonRewardWinner(2, 11, 21, "Bob", 90, 900));

        await ((IAtomicEffectHandler)new SeasonPlayerRewardsAtomicEffectHandler()).ApplyAsync(
            new SeasonPlayerRewardsAtomicEffect(seasonId), playerContext, CancellationToken.None);

        var clanContext = new AtomicScript { Wallet = new RecordingWallet() };
        clanContext.EnqueueSingle<string>(config);
        clanContext.EnqueueList(
            new ClanSeasonRewardWinner(1, 10, 30, "Clan", "CLN", 40, "Owner", 100, 1_000),
            new ClanSeasonRewardWinner(2, 11, 31, "Other", "OTH", 41, "Other", 90, 900));

        await ((IAtomicEffectHandler)new SeasonClanRewardsAtomicEffectHandler()).ApplyAsync(
            new SeasonClanRewardsAtomicEffect(seasonId), clanContext, CancellationToken.None);

        var playerResult = Assert.IsType<SeasonRewardProcessResult>(playerContext.Outputs["result"]);
        var clanResult = Assert.IsType<SeasonRewardProcessResult>(clanContext.Outputs["result"]);
        var rejectedContext = new AtomicScript { Wallet = new RecordingWallet { Applied = false } };
        rejectedContext.EnqueueSingle<string>(config);
        rejectedContext.EnqueueList(new PlayerSeasonRewardWinner(1, 10, 20, "Alice", 100, 1_000));
        var rejected = await Record.ExceptionAsync(() =>
            ((IAtomicEffectHandler)new SeasonPlayerRewardsAtomicEffectHandler()).ApplyAsync(
                new SeasonPlayerRewardsAtomicEffect(seasonId), rejectedContext, CancellationToken.None));
        var valid = playerResult.Paid == 1
            && clanResult.Paid == 1
            && ((RecordingWallet)playerContext.Wallet!).Mutations.Single().Effect.Amount == amount
            && ((RecordingWallet)clanContext.Wallet!).Mutations.Single().Effect.Amount == amount
            && rejected is InvalidOperationException
            && playerContext.Sql.Count(sql => sql.Contains("INSERT INTO meta_event_log", StringComparison.Ordinal)) == 1
            && clanContext.Sql.Count(sql => sql.Contains("INSERT INTO meta_event_log", StringComparison.Ordinal)) == 1;

        return valid
            .ToProperty()
            .Label($"season={seasonId}, amount={amount}, playerPaid={playerResult.Paid}, clanPaid={clanResult.Paid}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> SeasonRewardEffects_MissingConfigShortCircuits(PositiveInt raw)
    {
        var playerContext = new AtomicScript();
        playerContext.EnqueueSingle<string>(null);
        await ((IAtomicEffectHandler)new SeasonPlayerRewardsAtomicEffectHandler()).ApplyAsync(
            new SeasonPlayerRewardsAtomicEffect(raw.Get), playerContext, CancellationToken.None);

        var clanContext = new AtomicScript();
        clanContext.EnqueueSingle<string>(null);
        await ((IAtomicEffectHandler)new SeasonClanRewardsAtomicEffectHandler()).ApplyAsync(
            new SeasonClanRewardsAtomicEffect(raw.Get), clanContext, CancellationToken.None);

        var player = Assert.IsType<SeasonRewardProcessResult>(playerContext.Outputs["result"]);
        var clan = Assert.IsType<SeasonRewardProcessResult>(clanContext.Outputs["result"]);
        return (player.Paid == 0 && clan.Paid == 0
                && playerContext.QueryCalls == 1 && clanContext.QueryCalls == 1
                && playerContext.ExecuteCalls == 0 && clanContext.ExecuteCalls == 0)
            .ToProperty()
            .Label($"season={raw.Get}, playerQueries={playerContext.QueryCalls}, clanQueries={clanContext.QueryCalls}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> QuestEffects_CoverMissingProgressClaimAndRewardPaths(PositiveInt raw)
    {
        var season = Season(raw.Get, "{}");
        var quest = Quest(raw.Get);
        var completion = new GameCompletedMetaEvent(10, 20, "Alice", "dice", 100, 200, true, 2, 1);

        var progressContext = new AtomicScript();
        progressContext.EnqueueSingle<MetaSeason>(season);
        progressContext.EnqueueSingle<QuestPlayerProgress>(null);
        progressContext.EnqueueSingle(new QuestProgressUpdate(quest.Id, 3, quest.Target, true));
        var progressHandler = new QuestProgressAtomicEffectHandler(new FixedQuestCatalog(quest, quest));
        await ((IAtomicEffectHandler)progressHandler).ApplyAsync(
            new QuestProgressAtomicEffect(raw.Get, 10, 20, completion, DateTimeOffset.UnixEpoch),
            progressContext, CancellationToken.None);

        var progress = Assert.IsAssignableFrom<IReadOnlyList<QuestProgressUpdate>>(progressContext.Outputs["updates"]);

        var missingSeasonClaim = new AtomicScript();
        missingSeasonClaim.EnqueueSingle<MetaSeason>(null);
        await ((IAtomicEffectHandler)new QuestClaimAtomicEffectHandler(new FixedQuestCatalog(quest, quest))).ApplyAsync(
            new QuestClaimAtomicEffect(raw.Get, 10, 20, "Alice", quest.Id, DateTimeOffset.UnixEpoch),
            missingSeasonClaim, CancellationToken.None);

        var missingProgressContext = new AtomicScript();
        missingProgressContext.EnqueueSingle<MetaSeason>(null);
        await ((IAtomicEffectHandler)new QuestProgressAtomicEffectHandler(new FixedQuestCatalog(quest, quest))).ApplyAsync(
            new QuestProgressAtomicEffect(raw.Get, 10, 20, completion, DateTimeOffset.UnixEpoch),
            missingProgressContext, CancellationToken.None);

        var noQuestContext = new AtomicScript();
        noQuestContext.EnqueueSingle<MetaSeason>(season);
        noQuestContext.EnqueueSingle<QuestPlayerProgress>(null);
        await ((IAtomicEffectHandler)new QuestClaimAtomicEffectHandler(new FixedQuestCatalog(quest, null))).ApplyAsync(
            new QuestClaimAtomicEffect(raw.Get, 10, 20, "Alice", quest.Id, DateTimeOffset.UnixEpoch),
            noQuestContext, CancellationToken.None);

        var notClaimedContext = new AtomicScript();
        notClaimedContext.EnqueueSingle<MetaSeason>(season);
        notClaimedContext.EnqueueSingle<QuestPlayerProgress>(null);
        notClaimedContext.EnqueueSingle<string>(null);
        await ((IAtomicEffectHandler)new QuestClaimAtomicEffectHandler(new FixedQuestCatalog(quest, quest))).ApplyAsync(
            new QuestClaimAtomicEffect(raw.Get, 10, 20, "Alice", quest.Id, DateTimeOffset.UnixEpoch),
            notClaimedContext, CancellationToken.None);

        var rewardContext = new AtomicScript { Wallet = new RecordingWallet() };
        rewardContext.EnqueueSingle<MetaSeason>(season);
        rewardContext.EnqueueSingle<QuestPlayerProgress>(null);
        rewardContext.EnqueueSingle<string>(quest.Id);
        rewardContext.EnqueueSingle<string>(season.ConfigJson);
        rewardContext.EnqueueSingle(new SeasonPlayer(
            raw.Get, 10, 20, "Alice", quest.RewardXp, 1, 1_000, 1, 1, 0, 100, 200, DateTimeOffset.UnixEpoch));
        await ((IAtomicEffectHandler)new QuestClaimAtomicEffectHandler(new FixedQuestCatalog(quest, quest))).ApplyAsync(
            new QuestClaimAtomicEffect(raw.Get, 10, 20, "Alice", quest.Id, DateTimeOffset.UnixEpoch),
            rewardContext, CancellationToken.None);

        var noSeasonUpdates = Assert.IsAssignableFrom<IReadOnlyList<QuestProgressUpdate>>(missingProgressContext.Outputs["updates"]);
        var noQuest = noQuestContext.Outputs["result"];
        var notClaimed = Assert.IsType<QuestClaimResult>(notClaimedContext.Outputs["result"]);
        var claimed = Assert.IsType<QuestClaimResult>(rewardContext.Outputs["result"]);
        var rejectedRewardContext = new AtomicScript { Wallet = new RecordingWallet { Applied = false } };
        rejectedRewardContext.EnqueueSingle<MetaSeason>(season);
        rejectedRewardContext.EnqueueSingle<QuestPlayerProgress>(null);
        rejectedRewardContext.EnqueueSingle<string>(quest.Id);
        var rejectedReward = await Record.ExceptionAsync(() =>
            ((IAtomicEffectHandler)new QuestClaimAtomicEffectHandler(new FixedQuestCatalog(quest, quest))).ApplyAsync(
                new QuestClaimAtomicEffect(raw.Get, 10, 20, "Alice", quest.Id, DateTimeOffset.UnixEpoch),
                rejectedRewardContext, CancellationToken.None));
        var missingWalletClaim = new AtomicScript();
        missingWalletClaim.EnqueueSingle<MetaSeason>(season);
        missingWalletClaim.EnqueueSingle<QuestPlayerProgress>(null);
        missingWalletClaim.EnqueueSingle<string>(quest.Id);
        var missingWalletException = await Record.ExceptionAsync(() =>
            ((IAtomicEffectHandler)new QuestClaimAtomicEffectHandler(new FixedQuestCatalog(quest, quest))).ApplyAsync(
                new QuestClaimAtomicEffect(raw.Get, 10, 20, "Alice", quest.Id, DateTimeOffset.UnixEpoch),
                missingWalletClaim, CancellationToken.None));
        var missingXpPlayerContext = new AtomicScript { Wallet = new RecordingWallet() };
        missingXpPlayerContext.EnqueueSingle<MetaSeason>(season);
        missingXpPlayerContext.EnqueueSingle<QuestPlayerProgress>(null);
        missingXpPlayerContext.EnqueueSingle<string>(quest.Id);
        missingXpPlayerContext.EnqueueSingle<string>(season.ConfigJson);
        missingXpPlayerContext.EnqueueSingle<SeasonPlayer>(null);
        var missingXpPlayer = await Record.ExceptionAsync(() =>
            ((IAtomicEffectHandler)new QuestClaimAtomicEffectHandler(new FixedQuestCatalog(quest, quest))).ApplyAsync(
                new QuestClaimAtomicEffect(raw.Get, 10, 20, "Alice", quest.Id, DateTimeOffset.UnixEpoch),
                missingXpPlayerContext, CancellationToken.None));
        var valid = progress.Count == 1
            && noSeasonUpdates.Count == 0
            && missingSeasonClaim.Outputs["result"] is null
            && noQuest is null
            && !notClaimed.Claimed
            && claimed.Claimed
            && ((RecordingWallet)rewardContext.Wallet!).Mutations.Count == 1
            && ((RecordingWallet)rewardContext.Wallet!).Mutations[0].Effect.Kind == WalletBatchEffectKind.Credit
            && rejectedReward is InvalidOperationException
            && missingWalletException is InvalidOperationException
            && missingXpPlayer is InvalidOperationException
            && rewardContext.Sql.Any(sql => sql.Contains("quest.claimed", StringComparison.Ordinal));

        return valid
            .ToProperty()
            .Label($"season={raw.Get}, progress={progress.Count}, claimed={claimed.Claimed}, wallet={((RecordingWallet)rewardContext.Wallet!).Mutations.Count}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> TournamentCreateAndJoinEffects_PreserveLifecycleGuards(PositiveInt raw)
    {
        var id = raw.Get;
        var tournament = Tournament(id, chatId: 10, status: "open", playerCount: 0, maxPlayers: 8, entryFee: 10);
        var createContext = new AtomicScript();
        createContext.EnqueueSingle<long?>(id + 1L);
        createContext.EnqueueSingle(tournament with { Id = id + 1L });
        await ((IAtomicEffectHandler)new TournamentCreateAtomicEffectHandler()).ApplyAsync(
            new TournamentCreateAtomicEffect(id, 10, 20, " /CUBE ", 10, 8), createContext, CancellationToken.None);
        var create = Assert.IsType<TournamentCreateResult>(createContext.Outputs["result"]);

        var failedCreateContext = new AtomicScript();
        failedCreateContext.EnqueueSingle<long?>(null);
        var failedCreate = await Record.ExceptionAsync(() =>
            ((IAtomicEffectHandler)new TournamentCreateAtomicEffectHandler()).ApplyAsync(
                new TournamentCreateAtomicEffect(id, 10, 20, "dice", 10, 8), failedCreateContext, CancellationToken.None));

        var scenarios = new List<bool>();
        for (var mode = 0; mode < 8; mode++)
        {
            var context = new AtomicScript { Wallet = new RecordingWallet { Applied = mode != 5 } };
            var walletAlreadyApplied = mode == 6;
            if (mode == 0)
            {
                context.EnqueueSingle<TournamentInfo>(null);
            }
            else
            {
                var current = mode switch
                {
                    1 => tournament with { ChatId = 99 },
                    2 => tournament with { Status = "started" },
                    3 => tournament with { PlayerCount = tournament.MaxPlayers },
                    _ => tournament,
                };
                context.EnqueueSingle(current);
                if (mode is 4 or 5 or 6 or 7)
                    context.EnqueueSingle(mode == 4 ? 1 : 0);
                if (mode is 6 or 7)
                    context.EnqueueSingle(tournament with { PlayerCount = 1 });
            }

            await ((IAtomicEffectHandler)new TournamentJoinAtomicEffectHandler()).ApplyAsync(
                new TournamentJoinAtomicEffect(id, 20, 10, "Alice", walletAlreadyApplied), context, CancellationToken.None);
            var result = Assert.IsType<TournamentJoinResult>(context.Outputs["result"]);
            var expectedJoined = mode is 6 or 7;
            scenarios.Add(result.Joined == expectedJoined
                && ((RecordingWallet)context.Wallet!).Mutations.Count == (mode is 5 or 7 && !walletAlreadyApplied ? 1 : 0));
        }

        var rejectedDebitContext = new AtomicScript { Wallet = new RecordingWallet { Rejected = true } };
        rejectedDebitContext.EnqueueSingle(tournament);
        rejectedDebitContext.EnqueueSingle(0);
        await ((IAtomicEffectHandler)new TournamentJoinAtomicEffectHandler()).ApplyAsync(
            new TournamentJoinAtomicEffect(id, 20, 10, "Alice", false), rejectedDebitContext, CancellationToken.None);
        var rejectedDebit = Assert.IsType<TournamentJoinResult>(rejectedDebitContext.Outputs["result"]);
        var freeEntryContext = new AtomicScript { Wallet = new RecordingWallet() };
        freeEntryContext.EnqueueSingle(tournament with { EntryFee = 0 });
        freeEntryContext.EnqueueSingle(0);
        freeEntryContext.EnqueueSingle(tournament with { PlayerCount = 1, EntryFee = 0 });
        await ((IAtomicEffectHandler)new TournamentJoinAtomicEffectHandler()).ApplyAsync(
            new TournamentJoinAtomicEffect(id, 20, 10, "Alice", false), freeEntryContext, CancellationToken.None);
        var freeEntry = Assert.IsType<TournamentJoinResult>(freeEntryContext.Outputs["result"]);
        var missingWalletContext = new AtomicScript();
        missingWalletContext.EnqueueSingle(tournament);
        missingWalletContext.EnqueueSingle(0);
        var missingWallet = await Record.ExceptionAsync(() =>
            ((IAtomicEffectHandler)new TournamentJoinAtomicEffectHandler()).ApplyAsync(
                new TournamentJoinAtomicEffect(id, 20, 10, "Alice", false), missingWalletContext, CancellationToken.None));

        return (create.Created
                && failedCreate is InvalidOperationException
                && scenarios.All(static x => x)
                && rejectedDebit is TournamentJoinResult { Joined: false }
                && freeEntry is TournamentJoinResult { Joined: true }
                && ((RecordingWallet)freeEntryContext.Wallet!).Mutations.Count == 0
                && missingWallet is InvalidOperationException)
            .ToProperty()
            .Label($"tournament={id}, created={create.Created}, joinScenarios={scenarios.Count}, checks={string.Join(',', scenarios)}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> TournamentStartEffects_CreateBracketsAndHandleByes(PositiveInt raw)
    {
        var id = raw.Get;
        var checks = new List<bool>();
        for (var mode = 0; mode < 6; mode++)
        {
            var context = new AtomicScript();
            if (mode == 0)
            {
                context.EnqueueSingle<TournamentInfo>(null);
            }
            else
            {
                var current = mode switch
                {
                    1 => Tournament(id, 10, "open", 2, 8, 20),
                    2 => Tournament(id, 10, "started", 2, 8, 20),
                    3 => Tournament(id, 10, "open", 2, 8, 20),
                    _ => Tournament(id, 10, "open", 2, 8, 20),
                };
                if (mode == 1) current = current with { CreatedBy = 999 };
                context.EnqueueSingle(current);
                if (mode is 3 or 4 or 5)
                    context.EnqueueSingle(mode == 3 ? 1 : 0);
                if (mode is 4 or 5)
                {
                    var count = mode == 4 ? 1 : 2 + raw.Get % 2;
                    context.EnqueueList(Enumerable.Range(0, count)
                        .Select(index => new TournamentPlayerInfo(id, 20 + index, $"P{index}", "joined", DateTimeOffset.UnixEpoch))
                        .ToArray());
                }
            }

            await ((IAtomicEffectHandler)new TournamentStartAtomicEffectHandler()).ApplyAsync(
                new TournamentStartAtomicEffect(id, 20), context, CancellationToken.None);
            checks.Add(Equals(context.Outputs["result"], mode is 5));
        }

        var successContext = new AtomicScript();
        successContext.EnqueueSingle(Tournament(id, 10, "open", 3, 8, 20));
        successContext.EnqueueSingle(0);
        successContext.EnqueueList(
            new TournamentPlayerInfo(id, 20, "P1", "joined", DateTimeOffset.UnixEpoch),
            new TournamentPlayerInfo(id, 21, "P2", "joined", DateTimeOffset.UnixEpoch),
            new TournamentPlayerInfo(id, 22, "joined", "joined", DateTimeOffset.UnixEpoch));
        await ((IAtomicEffectHandler)new TournamentStartAtomicEffectHandler()).ApplyAsync(
            new TournamentStartAtomicEffect(id, 20), successContext, CancellationToken.None);

        var fivePlayersContext = new AtomicScript();
        fivePlayersContext.EnqueueSingle(Tournament(id, 10, "open", 5, 8, 20));
        fivePlayersContext.EnqueueSingle(0);
        fivePlayersContext.EnqueueList(Enumerable.Range(0, 5)
            .Select(index => new TournamentPlayerInfo(id, 20 + index, $"P{index}", "joined", DateTimeOffset.UnixEpoch))
            .ToArray());
        await ((IAtomicEffectHandler)new TournamentStartAtomicEffectHandler()).ApplyAsync(
            new TournamentStartAtomicEffect(id, 20), fivePlayersContext, CancellationToken.None);

        var singleByeContext = new AtomicScript();
        singleByeContext.EnqueueSingle(Tournament(id, 10, "open", 2, 8, 20));
        singleByeContext.EnqueueSingle(0);
        singleByeContext.EnqueueCustomList(new SingleByePlayersList(id));
        await ((IAtomicEffectHandler)new TournamentStartAtomicEffectHandler()).ApplyAsync(
            new TournamentStartAtomicEffect(id, 20), singleByeContext, CancellationToken.None);

        return (checks.All(static x => x)
                && Equals(successContext.Outputs["result"], true)
                && successContext.ExecuteCalls >= 8
                && successContext.Sql.Any(sql => sql.Contains("meta_tournament_matches", StringComparison.Ordinal))
                && Equals(fivePlayersContext.Outputs["result"], true)
                && Equals(singleByeContext.Outputs["result"], true))
            .ToProperty()
            .Label($"tournament={id}, scenarios={checks.Count}, successSql={successContext.ExecuteCalls}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> TournamentReportFinishAndCancelEffects_RespectOwnershipAndPayments(PositiveInt raw)
    {
        var id = raw.Get;
        var match = Match(id + 1, id, 1, 1, "ready", 20, "Alice", 21, "Bob");
        var started = Tournament(id, 10, "started", 2, 8, 10) with { PrizePool = 100 };
        var reportChecks = new List<bool>();
        for (var mode = 0; mode < 7; mode++)
        {
            var context = new AtomicScript { Wallet = new RecordingWallet() };
            if (mode == 0)
                context.EnqueueSingle<TournamentMatchInfo>(null);
            else
            {
                context.EnqueueSingle(mode == 2 ? match with { Status = "finished" } : match);
                context.EnqueueSingle(mode == 1 ? started with { CreatedBy = 999 } : started);
                if (mode >= 4)
                {
                    context.EnqueueSingle(mode == 4 ? 2 : 1);
                    context.EnqueueSingle(match with { Status = "finished", VictorUserId = 20 });
                    context.EnqueueSingle(new TournamentPlayerInfo(id, 20, "Alice", "winner", DateTimeOffset.UnixEpoch));
                }
            }

            var victor = mode == 3 ? 999 : 20;
            await ((IAtomicEffectHandler)new TournamentReportAtomicEffectHandler()).ApplyAsync(
                new TournamentReportAtomicEffect(id + 1, 20, victor), context, CancellationToken.None);
            var result = Assert.IsType<TournamentReportResult>(context.Outputs["result"]);
            reportChecks.Add(result.Updated == (mode is 4 or 5 or 6));
        }

        var finishChecks = new List<bool>();
        for (var mode = 0; mode < 4; mode++)
        {
            var context = new AtomicScript { Wallet = new RecordingWallet() };
            context.EnqueueSingle(mode == 0 ? null : started);
            if (mode != 0)
                context.EnqueueSingle(mode == 1 ? null : new TournamentPlayerInfo(id, 20, "Alice", mode == 2 ? "eliminated" : "joined", DateTimeOffset.UnixEpoch));
            if (mode == 3)
                context.EnqueueSingle(new TournamentPlayerInfo(id, 20, "Alice", "joined", DateTimeOffset.UnixEpoch));
            await ((IAtomicEffectHandler)new TournamentFinishAtomicEffectHandler()).ApplyAsync(
                new TournamentFinishAtomicEffect(id, mode == 3 ? 20 : 999, 20), context, CancellationToken.None);
            finishChecks.Add(context.Outputs["result"] is TournamentPlayerInfo == (mode == 3));
        }

        var cancelChecks = new List<bool>();
        for (var mode = 0; mode < 4; mode++)
        {
            var context = new AtomicScript { Wallet = new RecordingWallet() };
            context.EnqueueSingle(mode == 0 ? null : started);
            if (mode != 0)
            {
                if (mode == 1) context.Singles[typeof(TournamentInfo)].Clear();
                context.EnqueueList(new TournamentPlayerInfo(id, 20, "Alice", "joined", DateTimeOffset.UnixEpoch));
            }
            await ((IAtomicEffectHandler)new TournamentCancelAtomicEffectHandler()).ApplyAsync(
                new TournamentCancelAtomicEffect(id, mode == 1 ? 999 : 20, mode == 3), context, CancellationToken.None);
            cancelChecks.Add(context.Outputs["result"] is IReadOnlyList<TournamentPlayerInfo> == (mode is 2 or 3));
        }

        var rejectedFinish = new AtomicScript { Wallet = new RecordingWallet { Applied = false } };
        rejectedFinish.EnqueueSingle(started);
        rejectedFinish.EnqueueSingle(new TournamentPlayerInfo(id, 20, "Alice", "joined", DateTimeOffset.UnixEpoch));
        var rejectedPrize = await Record.ExceptionAsync(() =>
            ((IAtomicEffectHandler)new TournamentFinishAtomicEffectHandler()).ApplyAsync(
                new TournamentFinishAtomicEffect(id, 20, 20), rejectedFinish, CancellationToken.None));

        var wrongOwnerCancel = new AtomicScript();
        wrongOwnerCancel.EnqueueSingle(started with { CreatedBy = 999 });
        await ((IAtomicEffectHandler)new TournamentCancelAtomicEffectHandler()).ApplyAsync(
            new TournamentCancelAtomicEffect(id, 20, true), wrongOwnerCancel, CancellationToken.None);
        var wrongStatusCancel = new AtomicScript();
        wrongStatusCancel.EnqueueSingle(started with { Status = "finished" });
        await ((IAtomicEffectHandler)new TournamentCancelAtomicEffectHandler()).ApplyAsync(
            new TournamentCancelAtomicEffect(id, 20, true), wrongStatusCancel, CancellationToken.None);

        return (reportChecks.All(static x => x)
                && finishChecks.All(static x => x)
                && cancelChecks.All(static x => x)
                && rejectedPrize is InvalidOperationException
                && wrongOwnerCancel.Outputs["result"] is null
                && wrongStatusCancel.Outputs["result"] is null)
            .ToProperty()
            .Label($"tournament={id}, reports={reportChecks.Count}, finishes={finishChecks.Count}, cancels={cancelChecks.Count}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> MetaSeasonAdminEffects_CoverLifecycleAndHistory(PositiveInt raw)
    {
        var id = raw.Get;
        var createContext = new AdminScript();
        createContext.EnqueueSingle<long?>(id);
        await ((IAdminEffectHandler)new MetaSeasonCreateAdminEffectHandler()).ApplyAsync(
            new MetaSeasonCreateAdminEffect("Season", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "{}"),
            createContext, CancellationToken.None);
        var failedCreateContext = new AdminScript();
        failedCreateContext.EnqueueSingle<long?>(null);
        var failedCreate = await Record.ExceptionAsync(() =>
            ((IAdminEffectHandler)new MetaSeasonCreateAdminEffectHandler()).ApplyAsync(
                new MetaSeasonCreateAdminEffect("Season", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "{}"),
                failedCreateContext,
                CancellationToken.None));

        var prepareContext = new AdminScript();
        prepareContext.EnqueueSingle(0);
        prepareContext.EnqueueSingle(DateTimeOffset.UnixEpoch);
        prepareContext.EnqueueSingle(1);
        await ((IAdminEffectHandler)new MetaSeasonPrepareAdminEffectHandler()).ApplyAsync(
            new MetaSeasonPrepareAdminEffect(2, 14), prepareContext, CancellationToken.None);

        var activateContext = new AdminScript();
        activateContext.EnqueueExecute(1);
        activateContext.EnqueueExecute(raw.Get % 2);
        await ((IAdminEffectHandler)new MetaSeasonActivateAdminEffectHandler()).ApplyAsync(
            new MetaSeasonActivateAdminEffect(id), activateContext, CancellationToken.None);

        var finishContext = new AdminScript();
        finishContext.EnqueueExecute(raw.Get % 2);
        await ((IAdminEffectHandler)new MetaSeasonFinishAdminEffectHandler()).ApplyAsync(
            new MetaSeasonFinishAdminEffect(id), finishContext, CancellationToken.None);

        var configContext = new AdminScript();
        configContext.EnqueueExecute(raw.Get % 2);
        await ((IAdminEffectHandler)new MetaSeasonConfigAdminEffectHandler()).ApplyAsync(
            new MetaSeasonConfigAdminEffect(id, "{}", raw.Get % 2 == 0), configContext, CancellationToken.None);

        var changed = raw.Get % 2;
        var valid = Equals(createContext.Outputs["seasonId"], (long)id)
            && failedCreate is InvalidOperationException
            && prepareContext.Outputs["created"] is 2
            && Equals(activateContext.Outputs["changed"], changed)
            && Equals(finishContext.Outputs["changed"], changed)
            && Equals(configContext.Outputs["changed"], changed)
            && createContext.Sql.Any(sql => sql.Contains("meta_event_log", StringComparison.Ordinal))
            && prepareContext.Sql.Any(sql => sql.Contains("meta_event_log", StringComparison.Ordinal))
            && activateContext.Sql.Count(sql => sql.Contains("meta_event_log", StringComparison.Ordinal)) == (changed > 0 ? 1 : 0)
            && finishContext.Sql.Count(sql => sql.Contains("meta_event_log", StringComparison.Ordinal)) == (changed > 0 ? 1 : 0)
            && configContext.Sql.Count(sql => sql.Contains("meta_event_log", StringComparison.Ordinal)) == (changed > 0 ? 1 : 0);

        return valid
            .ToProperty()
            .Label($"season={id}, changed={changed}, prepared={prepareContext.Outputs["created"]}, createId={createContext.Outputs["seasonId"]}, activateHistory={activateContext.Sql.Count(sql => sql.Contains("meta_event_log", StringComparison.Ordinal))}, finishHistory={finishContext.Sql.Count(sql => sql.Contains("meta_event_log", StringComparison.Ordinal))}, configHistory={configContext.Sql.Count(sql => sql.Contains("meta_event_log", StringComparison.Ordinal))}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> MetaSeasonRewardAdminEffects_PayPlayersAndClans(PositiveInt raw)
    {
        var amount = 1 + raw.Get % 10_000;
        var config = RewardsJson(amount);
        var players = new AdminScript { Wallet = new RecordingWallet() };
        players.EnqueueSingle<string>(config);
        players.EnqueueList(
            new PlayerSeasonRewardWinner(1, 10, 20, "Alice", 100, 1_000),
            new PlayerSeasonRewardWinner(2, 11, 21, "Bob", 90, 900));
        await ((IAdminEffectHandler)new MetaSeasonPlayerRewardsAdminEffectHandler()).ApplyAsync(
            new MetaSeasonPlayerRewardsAdminEffect(raw.Get), players, CancellationToken.None);

        var clans = new AdminScript { Wallet = new RecordingWallet() };
        clans.EnqueueSingle<string>(config);
        clans.EnqueueList(
            new ClanSeasonRewardWinner(1, 10, 30, "Clan", "CLN", 40, "Owner", 100, 1_000),
            new ClanSeasonRewardWinner(2, 11, 31, "Other", "OTH", 41, "Other", 90, 900));
        await ((IAdminEffectHandler)new MetaSeasonClanRewardsAdminEffectHandler()).ApplyAsync(
            new MetaSeasonClanRewardsAdminEffect(raw.Get), clans, CancellationToken.None);

        var playerRows = Assert.IsAssignableFrom<IReadOnlyList<SeasonRewardPaidRow>>(players.Outputs["rows"]);
        var clanRows = Assert.IsAssignableFrom<IReadOnlyList<SeasonRewardPaidRow>>(clans.Outputs["rows"]);
        var emptyPlayers = new AdminScript();
        emptyPlayers.EnqueueSingle<string>(null);
        await ((IAdminEffectHandler)new MetaSeasonPlayerRewardsAdminEffectHandler()).ApplyAsync(
            new MetaSeasonPlayerRewardsAdminEffect(raw.Get), emptyPlayers, CancellationToken.None);
        var emptyClans = new AdminScript();
        emptyClans.EnqueueSingle<string>(null);
        await ((IAdminEffectHandler)new MetaSeasonClanRewardsAdminEffectHandler()).ApplyAsync(
            new MetaSeasonClanRewardsAdminEffect(raw.Get), emptyClans, CancellationToken.None);
        var rejectedPlayers = new AdminScript { Wallet = new RecordingWallet { Applied = false } };
        rejectedPlayers.EnqueueSingle<string>(config);
        rejectedPlayers.EnqueueList(new PlayerSeasonRewardWinner(1, 10, 20, "Alice", 100, 1_000));
        var rejected = await Record.ExceptionAsync(() =>
            ((IAdminEffectHandler)new MetaSeasonPlayerRewardsAdminEffectHandler()).ApplyAsync(
                new MetaSeasonPlayerRewardsAdminEffect(raw.Get), rejectedPlayers, CancellationToken.None));
        return (playerRows.Count == 1
                && clanRows.Count == 1
                && ((RecordingWallet)players.Wallet!).Mutations.Single().Effect.Amount == amount
                && ((RecordingWallet)clans.Wallet!).Mutations.Single().Effect.Amount == amount
                && players.Sql.Any(sql => sql.Contains("meta_event_log", StringComparison.Ordinal))
                && clans.Sql.Any(sql => sql.Contains("meta_event_log", StringComparison.Ordinal))
                && emptyPlayers.Outputs["rows"] is IReadOnlyList<SeasonRewardPaidRow> emptyPlayerRows && emptyPlayerRows.Count == 0
                && emptyClans.Outputs["rows"] is IReadOnlyList<SeasonRewardPaidRow> emptyClanRows && emptyClanRows.Count == 0
                && rejected is InvalidOperationException)
            .ToProperty()
            .Label($"season={raw.Get}, amount={amount}, playerRows={playerRows.Count}, clanRows={clanRows.Count}");
    }

    [Property(MaxTest = 20)]
    public async Task<Property> MetaCatalogAdminEffects_HandleReloadAndCancelledSave(PositiveInt raw)
    {
        var reload = new MetaQuestCatalogReloadAdminEffectHandler(JsonQuestCatalog.Default);
        await ((IAdminEffectHandler)reload).ApplyAsync(new MetaQuestCatalogReloadAdminEffect(), new AdminScript(), CancellationToken.None);
        var noOpReload = new MetaQuestCatalogReloadAdminEffectHandler(new FixedQuestCatalog(Quest(raw.Get), Quest(raw.Get)));
        await ((IAdminEffectHandler)noOpReload).ApplyAsync(new MetaQuestCatalogReloadAdminEffect(), new AdminScript(), CancellationToken.None);

        var save = new MetaQuestCatalogSaveAdminEffectHandler();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var exception = await Record.ExceptionAsync(() =>
            ((IAdminEffectHandler)save).ApplyAsync(
                new MetaQuestCatalogSaveAdminEffect($"{{\"seed\":{raw.Get}}}"),
                new AdminScript(),
                cancellation.Token));

        var path = JsonQuestCatalog.EditablePath();
        var original = File.Exists(path) ? await File.ReadAllTextAsync(path) : null;
        var saveContext = new AdminScript();
        try
        {
            await ((IAdminEffectHandler)save).ApplyAsync(
                new MetaQuestCatalogSaveAdminEffect($"{{\"seed\":{raw.Get}}}"),
                saveContext,
                CancellationToken.None);
        }
        finally
        {
            if (original is null)
            {
                if (File.Exists(path)) File.Delete(path);
            }
            else
            {
                await File.WriteAllTextAsync(path, original);
            }
        }

        return (exception is OperationCanceledException
                && Equals(saveContext.Outputs["path"], path))
            .ToProperty()
            .Label($"seed={raw.Get}, exception={exception?.GetType().Name ?? "none"}");
    }

    [Property(MaxTest = 100)]
    public async Task<Property> MetaEffectHelperBranches_CoverWalletAndAmountGuards(PositiveInt raw)
    {
        var amount = 1 + raw.Get % 10_000;
        var zeroSeason = new AtomicScript();
        await SeasonRewardsProbe.InvokeCreditAsync(zeroSeason, 0, CancellationToken.None);
        var acceptedSeason = new AtomicScript { Wallet = new RecordingWallet() };
        await SeasonRewardsProbe.InvokeCreditAsync(acceptedSeason, amount, CancellationToken.None);
        var rejectedSeason = new AtomicScript { Wallet = new RecordingWallet { Applied = false } };
        var rejectedSeasonException = await Record.ExceptionAsync(() =>
            SeasonRewardsProbe.InvokeCreditAsync(rejectedSeason, amount, CancellationToken.None));
        var missingSeasonWallet = await Record.ExceptionAsync(() =>
            SeasonRewardsProbe.InvokeCreditAsync(new AtomicScript(), amount, CancellationToken.None));

        var zeroTournament = new AtomicScript();
        await TournamentProbe.InvokeCreditAsync(zeroTournament, 0, CancellationToken.None);
        var missingTournamentWallet = await Record.ExceptionAsync(() =>
            TournamentProbe.InvokeCreditAsync(new AtomicScript(), amount, CancellationToken.None));
        var rejectedTournament = new AtomicScript { Wallet = new RecordingWallet { Applied = false } };
        var rejectedTournamentException = await Record.ExceptionAsync(() =>
            TournamentProbe.InvokeCreditAsync(rejectedTournament, amount, CancellationToken.None));
        var debitZero = await TournamentProbe.InvokeDebitAsync(new AtomicScript(), 0, CancellationToken.None);
        var debitAccepted = await TournamentProbe.InvokeDebitAsync(
            new AtomicScript { Wallet = new RecordingWallet() }, amount, CancellationToken.None);
        var debitRejected = await TournamentProbe.InvokeDebitAsync(
            new AtomicScript { Wallet = new RecordingWallet { Rejected = true } }, amount, CancellationToken.None);
        var debitNotApplied = await TournamentProbe.InvokeDebitAsync(
            new AtomicScript { Wallet = new RecordingWallet { Applied = false } }, amount, CancellationToken.None);
        var missingDebitWallet = await Record.ExceptionAsync(() =>
            TournamentProbe.InvokeDebitAsync(new AtomicScript(), amount, CancellationToken.None));
        var missingAdminWallet = await Record.ExceptionAsync(() =>
            AdminCreditProbe.InvokeCreditAsync(new AdminScript(), amount, CancellationToken.None));

        return (acceptedSeason.Wallet is RecordingWallet { Mutations.Count: 1 }
                && rejectedSeasonException is InvalidOperationException
                && missingSeasonWallet is InvalidOperationException
                && rejectedTournamentException is InvalidOperationException
                && missingTournamentWallet is InvalidOperationException
                && debitZero
                && debitAccepted
                && !debitRejected
                && !debitNotApplied
                && missingDebitWallet is InvalidOperationException
                && missingAdminWallet is InvalidOperationException)
            .ToProperty()
            .Label($"amount={amount}, debit={debitZero}/{debitAccepted}/{debitRejected}/{debitNotApplied}");
    }

    private static string RewardsJson(int amount) =>
        string.Create(CultureInfo.InvariantCulture, $"{{\"rewards\":{{\"playerTop\":[{amount}],\"clanTop\":[{amount}]}}}}");

    private static MetaSeason Season(long id, string config) =>
        new(id, $"Season {id}", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch.AddDays(14), "active", config);

    private static QuestTemplate Quest(long seed) =>
        new($"quest-{seed}", "Play", "Play", "daily", "games", "dice", 3, 100 + seed % 500, 10 + (int)(seed % 100));

    private static TournamentInfo Tournament(long id, long chatId, string status, int playerCount, int maxPlayers, int entryFee) =>
        new(id, 7, chatId, "dice", "single_elimination", status, entryFee, maxPlayers, 20, DateTimeOffset.UnixEpoch, playerCount, 100);

    private static TournamentMatchInfo Match(
        long matchId,
        long tournamentId,
        int round,
        int index,
        string status,
        long player1,
        string name1,
        long player2,
        string name2) =>
        new(matchId, tournamentId, round, index, status, player1, name1, player2, name2, null, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);

    private sealed class SeasonRewardsProbe : SeasonRewardsAtomicEffectHandler<SeasonPlayerRewardsAtomicEffect>
    {
        public static Task InvokeCreditAsync(IAtomicEffectContext context, int amount, CancellationToken ct) =>
            CreditAsync(context, 1, 2, "Alice", amount, "property.credit", "property:season-credit", ct);

        protected override Task ApplyAsync(SeasonPlayerRewardsAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class TournamentProbe : TournamentAtomicEffectHandler<TournamentCreateAtomicEffect>
    {
        public static Task<bool> InvokeDebitAsync(IAtomicEffectContext context, int amount, CancellationToken ct) =>
            TryDebitAsync(context, 1, 2, amount, "property.debit", "property:tournament-debit", ct);

        public static Task InvokeCreditAsync(IAtomicEffectContext context, int amount, CancellationToken ct) =>
            CreditAsync(context, 1, 2, "Alice", amount, "property.credit", "property:tournament-credit", ct);

        protected override Task ApplyAsync(TournamentCreateAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class AdminCreditProbe : MetaAdminEffectHandler<MetaSeasonPlayerRewardsAdminEffect>
    {
        public static Task InvokeCreditAsync(IAdminExecutionContext context, int amount, CancellationToken ct) =>
            CreditAsync(context, 1, 2, "Alice", amount, "property.credit", "property:admin-credit", ct);

        protected override Task ApplyAsync(MetaSeasonPlayerRewardsAdminEffect effect, IAdminExecutionContext context, CancellationToken ct) =>
            Task.CompletedTask;
    }

    private sealed class SingleByePlayersList(long tournamentId) : IReadOnlyList<TournamentPlayerInfo>
    {
        public int Count => 2;
        public TournamentPlayerInfo this[int index] => index == 0
            ? new TournamentPlayerInfo(tournamentId, 20, "P1", "joined", DateTimeOffset.UnixEpoch)
            : null!;
        public IEnumerator<TournamentPlayerInfo> GetEnumerator()
        {
            yield return this[0];
            yield return this[1];
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class FixedQuestCatalog(QuestTemplate matching, QuestTemplate? active) : IQuestCatalog
    {
        public IReadOnlyList<QuestTemplate> All => active is null ? [] : [active];
        public void Reload() { }
        public IReadOnlyList<QuestTemplate> ActiveFor(MetaSeason season, long chatId, long userId, DateTimeOffset now, QuestPlayerProgress? progress = null) =>
            active is null ? [] : [active];
        public IEnumerable<QuestTemplate> Matching(MetaSeason season, long chatId, long userId, GameCompletedMetaEvent ev, QuestPlayerProgress? progress = null) =>
            matching is null ? [] : [matching];
        public QuestTemplate? FindActive(MetaSeason season, long chatId, long userId, string questId, DateTimeOffset now, QuestPlayerProgress? progress = null) => active;
    }

    private sealed class RecordingWallet : IWalletAtomicExecutionService
    {
        public bool Applied { get; init; } = true;
        public bool Rejected { get; init; }
        public List<WalletMutation> Mutations { get; } = [];
        public int EnsureCalls { get; private set; }
        public Task EnsureUserAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct)
        {
            EnsureCalls++;
            return Task.CompletedTask;
        }
        public Task<int> GetBalanceAsync(long userId, long balanceScopeId, CancellationToken ct) => Task.FromResult(0);
        public Task<WalletBatchMutationResult> ApplyBatchAsync(long userId, long balanceScopeId, IReadOnlyList<WalletBatchEffect> effects, string operationId, CancellationToken ct)
        {
            Mutations.Add(new WalletMutation(userId, balanceScopeId, effects.Single(), operationId));
            return Task.FromResult(new WalletBatchMutationResult(Applied, Rejected, 0));
        }
    }

    private sealed record WalletMutation(long UserId, long ScopeId, WalletBatchEffect Effect, string OperationId);

    private sealed class AtomicScript : IAtomicEffectContext
    {
        public IWalletAtomicExecutionService? Wallet { get; init; }
        public Dictionary<Type, Queue<object?>> Singles { get; } = [];
        public Dictionary<Type, Queue<object>> Lists { get; } = [];
        public Queue<int> ExecuteResults { get; } = [];
        public Dictionary<string, object?> Outputs { get; } = new(StringComparer.Ordinal);
        public List<string> Sql { get; } = [];
        public int QueryCalls { get; private set; }
        public int ExecuteCalls { get; private set; }

        public void EnqueueSingle<T>(T? value)
        {
            if (!Singles.TryGetValue(typeof(T), out var queue)) Singles[typeof(T)] = queue = [];
            queue.Enqueue(value);
        }

        public void EnqueueList<T>(params T[] values)
        {
            if (!Lists.TryGetValue(typeof(T), out var queue)) Lists[typeof(T)] = queue = [];
            queue.Enqueue(values.Cast<object?>().ToArray());
        }

        public void EnqueueCustomList<T>(IReadOnlyList<T> values)
        {
            if (!Lists.TryGetValue(typeof(T), out var queue)) Lists[typeof(T)] = queue = [];
            queue.Enqueue(values);
        }

        public Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct)
        {
            ExecuteCalls++;
            Sql.Add(sql);
            return Task.FromResult(ExecuteResults.Count == 0 ? 1 : ExecuteResults.Dequeue());
        }

        public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct)
        {
            QueryCalls++;
            if (!Singles.TryGetValue(typeof(T), out var queue) || queue.Count == 0)
                return Task.FromResult<T?>(default);
            var value = queue.Dequeue();
            return Task.FromResult(value is null ? default : (T)value);
        }

        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct)
        {
            QueryCalls++;
            if (!Lists.TryGetValue(typeof(T), out var queue) || queue.Count == 0)
                return Task.FromResult<IReadOnlyList<T>>([]);
            var value = queue.Dequeue();
            return value is IReadOnlyList<T> typed
                ? Task.FromResult(typed)
                : Task.FromResult<IReadOnlyList<T>>(((IReadOnlyList<object?>)value).Cast<T>().ToArray());
        }

        public void SetOutput(string key, object? value) => Outputs[key] = value;
    }

    private sealed class AdminScript : IAdminExecutionContext
    {
        public AdminActor Actor { get; } = new(99, "property-admin");
        public string Action { get; } = "meta.property";
        public IWalletAtomicExecutionService? Wallet { get; init; }
        public Dictionary<Type, Queue<object?>> Singles { get; } = [];
        public Dictionary<Type, Queue<IReadOnlyList<object>>> Lists { get; } = [];
        public Queue<int> ExecuteResults { get; } = [];
        public Dictionary<string, object?> Outputs { get; } = new(StringComparer.Ordinal);
        public List<string> Sql { get; } = [];
        public int QueryCalls { get; private set; }
        public int ExecuteCalls { get; private set; }

        public void EnqueueSingle<T>(T? value)
        {
            if (!Singles.TryGetValue(typeof(T), out var queue)) Singles[typeof(T)] = queue = [];
            queue.Enqueue(value);
        }

        public void EnqueueList<T>(params T[] values)
        {
            if (!Lists.TryGetValue(typeof(T), out var queue)) Lists[typeof(T)] = queue = [];
            queue.Enqueue(values.Cast<object>().ToArray());
        }

        public void EnqueueExecute(int value) => ExecuteResults.Enqueue(value);

        public Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct)
        {
            ExecuteCalls++;
            Sql.Add(sql);
            return Task.FromResult(ExecuteResults.Count == 0 ? 1 : ExecuteResults.Dequeue());
        }

        public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct)
        {
            QueryCalls++;
            if (!Singles.TryGetValue(typeof(T), out var queue) || queue.Count == 0)
                return Task.FromResult<T?>(default);
            var value = queue.Dequeue();
            return Task.FromResult(value is null ? default : (T)value);
        }

        public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct)
        {
            QueryCalls++;
            if (!Lists.TryGetValue(typeof(T), out var queue) || queue.Count == 0)
                return Task.FromResult<IReadOnlyList<T>>([]);
            return Task.FromResult<IReadOnlyList<T>>(queue.Dequeue().Cast<T>().ToArray());
        }

        public void SetOutput(string key, object? value) => Outputs[key] = value;
    }
}
