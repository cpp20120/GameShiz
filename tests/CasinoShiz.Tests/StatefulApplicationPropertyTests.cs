using BotFramework.Sdk.Execution;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Games.Basketball.Application.Execution;
using Games.Blackjack.Application.Execution;
using Games.Bowling.Application.Execution;
using Games.Darts.Application.Execution;
using Games.DiceCube.Application.Execution;
using Games.Football.Application.Execution;
using Games.Pick.Application.Execution;
using Games.Pick.Infrastructure.Persistence;

namespace CasinoShiz.Tests;

public sealed class StatefulApplicationPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);

    [Property(MaxTest = 120)]
    public Property Blackjack_CommandSequence_PreservesTurnAndDeckInvariants(NonEmptyArray<int> commands)
    {
        var state = new BlackjackGameState(0, TurnGameStatus.Completed, 1, null, "pbt", null);
        var balance = 1_000L;
        var entropy = new EntropyValue(
            Enumerable.Range(1, 51).Select(index =>
                new KeyValuePair<string, double>($"shuffle-{index}", 0.37)));
        string? failure = CheckBlackjackInvariants(state, balance);

        foreach (var rawCommand in commands.Get)
        {
            if (failure is not null)
                break;

            var magnitude = Magnitude(rawCommand);
            if (state.Status == TurnGameStatus.Completed)
            {
                var start = new BlackjackStartAction().Decide(
                    new GameActionInput<BlackjackGameState, BlackjackStartCommand>(
                        new(1, "pbt", 10, 10, $"blackjack:{magnitude}:{rawCommand}", 1, 100, 60_000),
                        state,
                        new WalletSnapshot(balance),
                        new Dictionary<string, QuotaSnapshot>(),
                        entropy,
                        Now));
                failure = ApplyAcceptedBlackjack(start, ref state, ref balance);
            }
            else
            {
                var kind = (BlackjackTurnKind)(magnitude % 3);
                var turn = new BlackjackTurnAction().Decide(
                    new GameActionInput<BlackjackGameState, BlackjackTurnCommand>(
                        new(1, "pbt", 10, kind, state.Revision, $"blackjack:turn:{magnitude}"),
                        state,
                        new WalletSnapshot(balance),
                        new Dictionary<string, QuotaSnapshot>(),
                        EntropyValue.Empty,
                        Now));
                failure = ApplyAcceptedBlackjack(turn, ref state, ref balance);
            }
        }

        return ((failure ?? CheckBlackjackInvariants(state, balance)) is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, state={state}, balance={balance}");
    }

    [Property(MaxTest = 120)]
    public Property PickLottery_CommandSequence_PreservesPoolAndSettlementInvariants(NonEmptyArray<int> commands)
    {
        var state = new QuickLotteryState(null, []);
        var balances = new Dictionary<long, long>();
        string? failure = null;

        foreach (var rawCommand in commands.Get)
        {
            if (failure is not null || state.Row is { Status: not "open" })
                break;

            var magnitude = Magnitude(rawCommand);
            if (state.Row is null)
            {
                const long userId = 1;
                balances[userId] = GetBalance(balances, userId);
                var open = new QuickLotteryOpenAction().Decide(
                    new GameActionInput<QuickLotteryState, QuickLotteryOpenCommand>(
                        new(userId, "pbt-1", 10, 20, $"pick:open:{magnitude}:{rawCommand}", 1, 100, 300),
                        state,
                        new WalletSnapshot(balances[userId]),
                        new Dictionary<string, QuotaSnapshot>(),
                        EntropyValue.Empty,
                        Now));
                if (open.Status == DecisionStatus.Accepted)
                {
                    ApplyEconomy(open.Economy, balances, userId);
                    state = open.NewState;
                }
                else
                {
                    failure = "lottery open unexpectedly rejected";
                }
            }
            else if (magnitude % 3 == 0)
            {
                var settle = new QuickLotterySettleAction().Decide(
                    new GameActionInput<QuickLotteryState, QuickLotterySettleCommand>(
                        new(state.Row, state.Entries, magnitude % 2 == 0, $"pick:settle:{magnitude}", 2, 0.05),
                        state,
                        new WalletSnapshot(0),
                        new Dictionary<string, QuotaSnapshot>(),
                        new EntropyValue(new Dictionary<string, double>
                        {
                            [QuickLotterySettleAction.WinnerEntropy] = 0.37,
                        }),
                        Now));
                if (settle.Status != DecisionStatus.Accepted)
                {
                    failure = "lottery settlement unexpectedly rejected";
                }
                else
                {
                    ApplyCustomWalletCredits(settle.CustomEffects, balances);
                    state = settle.NewState;
                }
            }
            else
            {
                var userId = 2 + magnitude % 8;
                balances[userId] = GetBalance(balances, userId);
                var join = new QuickLotteryJoinAction().Decide(
                    new GameActionInput<QuickLotteryState, QuickLotteryJoinCommand>(
                        new(userId, $"pbt-{userId}", 10, $"pick:join:{magnitude}"),
                        state,
                        new WalletSnapshot(balances[userId]),
                        new Dictionary<string, QuotaSnapshot>(),
                        EntropyValue.Empty,
                        Now));
                if (join.Status == DecisionStatus.Accepted)
                {
                    ApplyEconomy(join.Economy, balances, userId);
                    state = join.NewState;
                }
            }

            failure = CheckLotteryInvariants(state, balances);
        }

        return ((failure ?? CheckLotteryInvariants(state, balances)) is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, state={state}");
    }

    [Property(MaxTest = 100)]
    public Property Basketball_CommandSequence_PreservesQueuedBetInvariants(NonEmptyArray<int> commands)
    {
        var state = new BasketballBetState(null);
        var balance = 1_000L;
        var quotaUsed = 0L;
        string? failure = null;

        foreach (var rawCommand in commands.Get)
        {
            var magnitude = Magnitude(rawCommand);
            var previousState = state;
            var previousBalance = balance;
            var previousQuota = quotaUsed;
            if (state.PendingBet is null)
            {
                var decision = new BasketballPlaceBetAction().Decide(
                    new GameActionInput<BasketballBetState, BasketballPlaceBetCommand>(
                        new(1, "pbt", 10, 1 + (int)(magnitude % 40), $"basketball:place:{magnitude}", 100, null),
                        state,
                        new WalletSnapshot(balance),
                        Quotas(BasketballPlaceBetAction.DailyRollQuota, quotaUsed),
                        EntropyValue.Empty,
                        Now));
                failure = ProcessQueuedDecision(decision, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }
            else if (magnitude % 3 == 0)
            {
                var abort = new BasketballAbortAction().Decide(
                    new GameActionInput<BasketballBetState, BasketballAbortCommand>(
                        new(1, "pbt", 10, $"basketball:abort:{magnitude}"),
                        state,
                        new WalletSnapshot(balance),
                        Quotas(BasketballPlaceBetAction.DailyRollQuota, quotaUsed),
                        EntropyValue.Empty,
                        Now));
                failure = ProcessQueuedDecision(abort, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }
            else
            {
                var roll = new BasketballThrowAction().Decide(
                    new GameActionInput<BasketballBetState, BasketballThrowCommand>(
                        new(1, "pbt", 10, 1 + (int)(magnitude % 6), $"basketball:throw:{magnitude}", 0),
                        state,
                        new WalletSnapshot(balance),
                        Quotas(BasketballPlaceBetAction.DailyRollQuota, quotaUsed),
                        new EntropyValue(new Dictionary<string, double> { [BasketballThrowAction.RedeemDropEntropy] = 0.99 }),
                        Now));
                failure = ProcessQueuedDecision(roll, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }

            if (failure is not null)
                break;

            failure = CheckQueuedInvariants(state.PendingBet?.UserId, state.PendingBet?.ChatId, state.PendingBet?.Amount, balance, quotaUsed);
            if (failure is not null)
                break;
        }

        return ((failure ?? CheckQueuedInvariants(state.PendingBet?.UserId, state.PendingBet?.ChatId, state.PendingBet?.Amount, balance, quotaUsed)) is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, state={state}, balance={balance}, quota={quotaUsed}");
    }

    [Property(MaxTest = 100)]
    public Property Bowling_CommandSequence_PreservesQueuedBetInvariants(NonEmptyArray<int> commands)
    {
        var state = new BowlingBetState(null);
        var balance = 1_000L;
        var quotaUsed = 0L;
        string? failure = null;

        foreach (var rawCommand in commands.Get)
        {
            var magnitude = Magnitude(rawCommand);
            var previousState = state;
            var previousBalance = balance;
            var previousQuota = quotaUsed;
            if (state.PendingBet is null)
            {
                var decision = new BowlingPlaceBetAction().Decide(
                    new GameActionInput<BowlingBetState, BowlingPlaceBetCommand>(
                        new(1, "pbt", 10, 1 + (int)(magnitude % 40), $"bowling:place:{magnitude}", 100, null), state,
                        new WalletSnapshot(balance), Quotas(BowlingPlaceBetAction.DailyRollQuota, quotaUsed), EntropyValue.Empty, Now));
                failure = ProcessQueuedDecision(decision, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }
            else if (magnitude % 3 == 0)
            {
                var abort = new BowlingAbortAction().Decide(
                    new GameActionInput<BowlingBetState, BowlingAbortCommand>(
                        new(1, "pbt", 10, $"bowling:abort:{magnitude}"), state, new WalletSnapshot(balance),
                        Quotas(BowlingPlaceBetAction.DailyRollQuota, quotaUsed), EntropyValue.Empty, Now));
                failure = ProcessQueuedDecision(abort, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }
            else
            {
                var roll = new BowlingRollAction().Decide(
                    new GameActionInput<BowlingBetState, BowlingRollCommand>(
                        new(1, "pbt", 10, 1 + (int)(magnitude % 6), $"bowling:roll:{magnitude}", 0), state,
                        new WalletSnapshot(balance), Quotas(BowlingPlaceBetAction.DailyRollQuota, quotaUsed),
                        new EntropyValue(new Dictionary<string, double> { [BowlingRollAction.RedeemDropEntropy] = 0.99 }), Now));
                failure = ProcessQueuedDecision(roll, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }

            if (failure is not null)
                break;

            failure = CheckQueuedInvariants(state.PendingBet?.UserId, state.PendingBet?.ChatId, state.PendingBet?.Amount, balance, quotaUsed);
            if (failure is not null)
                break;
        }

        return ((failure ?? CheckQueuedInvariants(state.PendingBet?.UserId, state.PendingBet?.ChatId, state.PendingBet?.Amount, balance, quotaUsed)) is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, state={state}, balance={balance}, quota={quotaUsed}");
    }

    [Property(MaxTest = 100)]
    public Property Football_CommandSequence_PreservesQueuedBetInvariants(NonEmptyArray<int> commands)
    {
        var state = new FootballBetState(null);
        var balance = 1_000L;
        var quotaUsed = 0L;
        string? failure = null;

        foreach (var rawCommand in commands.Get)
        {
            var magnitude = Magnitude(rawCommand);
            var previousState = state;
            var previousBalance = balance;
            var previousQuota = quotaUsed;
            if (state.PendingBet is null)
            {
                var decision = new FootballPlaceBetAction().Decide(
                    new GameActionInput<FootballBetState, FootballPlaceBetCommand>(
                        new(1, "pbt", 10, 1 + (int)(magnitude % 40), $"football:place:{magnitude}", 100, null), state,
                        new WalletSnapshot(balance), Quotas(FootballPlaceBetAction.DailyRollQuota, quotaUsed), EntropyValue.Empty, Now));
                failure = ProcessQueuedDecision(decision, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }
            else if (magnitude % 3 == 0)
            {
                var abort = new FootballAbortAction().Decide(
                    new GameActionInput<FootballBetState, FootballAbortCommand>(
                        new(1, "pbt", 10, $"football:abort:{magnitude}"), state, new WalletSnapshot(balance),
                        Quotas(FootballPlaceBetAction.DailyRollQuota, quotaUsed), EntropyValue.Empty, Now));
                failure = ProcessQueuedDecision(abort, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }
            else
            {
                var roll = new FootballThrowAction().Decide(
                    new GameActionInput<FootballBetState, FootballThrowCommand>(
                        new(1, "pbt", 10, 1 + (int)(magnitude % 6), $"football:throw:{magnitude}", 0), state,
                        new WalletSnapshot(balance), Quotas(FootballPlaceBetAction.DailyRollQuota, quotaUsed),
                        new EntropyValue(new Dictionary<string, double> { [FootballThrowAction.RedeemDropEntropy] = 0.99 }), Now));
                failure = ProcessQueuedDecision(roll, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }

            if (failure is not null)
                break;

            failure = CheckQueuedInvariants(state.PendingBet?.UserId, state.PendingBet?.ChatId, state.PendingBet?.Amount, balance, quotaUsed);
            if (failure is not null)
                break;
        }

        return ((failure ?? CheckQueuedInvariants(state.PendingBet?.UserId, state.PendingBet?.ChatId, state.PendingBet?.Amount, balance, quotaUsed)) is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, state={state}, balance={balance}, quota={quotaUsed}");
    }

    [Property(MaxTest = 100)]
    public Property DiceCube_CommandSequence_PreservesQueuedBetInvariants(NonEmptyArray<int> commands)
    {
        var state = new DiceCubePlaceBetState(null);
        var balance = 1_000L;
        var quotaUsed = 0L;
        string? failure = null;

        foreach (var rawCommand in commands.Get)
        {
            var magnitude = Magnitude(rawCommand);
            var previousState = state;
            var previousBalance = balance;
            var previousQuota = quotaUsed;
            if (state.PendingBet is null)
            {
                var decision = new DiceCubePlaceBetAction().Decide(
                    new GameActionInput<DiceCubePlaceBetState, DiceCubePlaceBetCommand>(
                        new(1, "pbt", 10, 1 + (int)(magnitude % 40), $"dicecube:place:{magnitude}", 100, 1, 2, 3, 0, null), state,
                        new WalletSnapshot(balance), Quotas(DiceCubePlaceBetAction.DailyRollQuota, quotaUsed), EntropyValue.Empty, Now));
                failure = ProcessQueuedDecision(decision, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }
            else if (magnitude % 3 == 0)
            {
                var abort = new DiceCubeAbortAction().Decide(
                    new GameActionInput<DiceCubePlaceBetState, DiceCubeAbortCommand>(
                        new(1, "pbt", 10, $"dicecube:abort:{magnitude}"), state, new WalletSnapshot(balance),
                        Quotas(DiceCubePlaceBetAction.DailyRollQuota, quotaUsed), EntropyValue.Empty, Now));
                failure = ProcessQueuedDecision(abort, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }
            else
            {
                var roll = new DiceCubeRollAction().Decide(
                    new GameActionInput<DiceCubePlaceBetState, DiceCubeRollCommand>(
                        new(1, "pbt", 10, 1 + (int)(magnitude % 6), $"dicecube:roll:{magnitude}", 0), state,
                        new WalletSnapshot(balance), Quotas(DiceCubePlaceBetAction.DailyRollQuota, quotaUsed),
                        new EntropyValue(new Dictionary<string, double> { [DiceCubeRollAction.RedeemDropEntropy] = 0.99 }), Now));
                failure = ProcessQueuedDecision(roll, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }

            if (failure is not null)
                break;

            failure = CheckQueuedInvariants(state.PendingBet?.UserId, state.PendingBet?.ChatId, state.PendingBet?.Amount, balance, quotaUsed);
            if (failure is not null)
                break;
        }

        return ((failure ?? CheckQueuedInvariants(state.PendingBet?.UserId, state.PendingBet?.ChatId, state.PendingBet?.Amount, balance, quotaUsed)) is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, state={state}, balance={balance}, quota={quotaUsed}");
    }

    [Property(MaxTest = 100)]
    public Property Darts_CommandSequence_PreservesQueuedRoundInvariants(NonEmptyArray<int> commands)
    {
        var state = new DartsQueuedState(null, 0);
        var balance = 1_000L;
        var quotaUsed = 0L;
        string? failure = null;

        foreach (var rawCommand in commands.Get)
        {
            var magnitude = Magnitude(rawCommand);
            var previousState = state;
            var previousBalance = balance;
            var previousQuota = quotaUsed;
            if (state.Round is null)
            {
                var place = new DartsPlaceBetAction().Decide(
                    new GameActionInput<DartsQueuedState, DartsPlaceBetCommand>(
                        new(1, "pbt", 10, 1 + (int)(magnitude % 40), 7, 1, $"darts:place:{magnitude}", 100, null),
                        state,
                        new WalletSnapshot(balance),
                        Quotas(DartsPlaceBetAction.DailyRollQuota, quotaUsed),
                        EntropyValue.Empty,
                        Now));
                failure = ProcessQueuedDecision(place, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }
            else if (state.Round.Status == DartsRoundStatus.Queued)
            {
                if (magnitude % 3 == 0)
                {
                    var abort = new DartsAbortRoundAction().Decide(
                        new GameActionInput<DartsQueuedState, DartsAbortRoundCommand>(
                            new(1, 1, "pbt", 10, $"darts:abort:{magnitude}"),
                            state,
                            new WalletSnapshot(balance),
                            Quotas(DartsPlaceBetAction.DailyRollQuota, quotaUsed),
                            EntropyValue.Empty,
                            Now));
                    failure = ProcessQueuedDecision(abort, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
                }
                else
                {
                    state = state with { Round = state.Round with { Status = DartsRoundStatus.AwaitingOutcome, BotMessageId = 500 } };
                }
            }
            else
            {
                var resolve = new DartsResolveRoundAction().Decide(
                    new GameActionInput<DartsQueuedState, DartsResolveRoundCommand>(
                        new(1, 1, "pbt", 10, magnitude % 5 == 0 ? 501 : 500, 1 + (int)(magnitude % 6), $"darts:resolve:{magnitude}", 0),
                        state,
                        new WalletSnapshot(balance),
                        Quotas(DartsPlaceBetAction.DailyRollQuota, quotaUsed),
                        new EntropyValue(new Dictionary<string, double> { [DartsResolveRoundAction.RedeemDropEntropy] = 0.99 }),
                        Now));
                failure = ProcessQueuedDecision(resolve, previousState, ref state, previousBalance, ref balance, previousQuota, ref quotaUsed);
            }

            if (failure is not null)
                break;
            failure = CheckDartsInvariants(state, balance, quotaUsed);
            if (failure is not null)
                break;
        }

        return ((failure ?? CheckDartsInvariants(state, balance, quotaUsed)) is null)
            .ToProperty()
            .Label(failure ?? $"commands={commands.Get.Length}, state={state}, balance={balance}, quota={quotaUsed}");
    }

    private static string? ApplyAcceptedBlackjack<TResult>(
        GameDecision<BlackjackGameState, TResult> decision,
        ref BlackjackGameState state,
        ref long balance)
    {
        if (decision.Status != DecisionStatus.Accepted)
        {
            if (decision.Economy.Count != 0 || decision.NewState != state)
                return "blackjack rejection mutated state or emitted economy effects";
            return CheckBlackjackInvariants(state, balance);
        }

        state = decision.NewState;
        balance = ApplyEconomy(decision.Economy, balance);
        return CheckBlackjackInvariants(state, balance);
    }

    private static string? CheckBlackjackInvariants(BlackjackGameState state, long balance)
    {
        if (balance < 0)
            return "blackjack balance became negative";
        if (state.Revision < 0)
            return "blackjack revision became negative";
        if (state.Status == TurnGameStatus.Active && state.Hand is null)
            return "active blackjack state has no hand";
        if (state.Status != TurnGameStatus.Active && state.Hand is not null)
            return "inactive blackjack state still has a hand";

        if (state.Hand is not { } hand)
            return null;
        if (hand.PlayerCards.Length < 2 || hand.DealerCards.Length < 2 || hand.Bet <= 0)
            return "blackjack hand shape is invalid";
        var cards = hand.PlayerCards.Concat(hand.DealerCards).Concat(SplitCards(hand.DeckState)).ToArray();
        if (cards.Length != 52 || cards.Distinct(StringComparer.Ordinal).Count() != cards.Length)
            return "blackjack deck lost or duplicated a card";
        return null;
    }

    private static string? CheckLotteryInvariants(QuickLotteryState state, IReadOnlyDictionary<long, long> balances)
    {
        if (balances.Values.Any(balance => balance < 0))
            return "lottery balance became negative";
        if (state.Row is not { } row)
            return state.Entries.Count == 0 ? null : "lottery has entries without a row";
        if (row.Status is not ("open" or "settled" or "cancelled"))
            return $"unknown lottery status '{row.Status}'";
        if (state.Entries.Any(entry => entry.LotteryId != row.Id || entry.StakePaid != row.Stake))
            return "lottery entry does not belong to the pool";
        if (state.Entries.Select(entry => entry.UserId).Distinct().Count() != state.Entries.Count)
            return "lottery contains duplicate players";
        if (row.Status == "open" && row.WinnerId is not null)
            return "open lottery already has a winner";
        if (row.Status == "settled")
        {
            if (row.WinnerId is null || !state.Entries.Any(entry => entry.UserId == row.WinnerId))
                return "settled lottery has no participating winner";
            if (row.PotTotal != state.Entries.Sum(entry => entry.StakePaid)
                || row.Payout is null || row.Fee is null
                || row.Payout + row.Fee != row.PotTotal)
                return "settled lottery totals do not balance";
        }
        return null;
    }

    private static string? CheckQueuedInvariants(long? userId, long? chatId, int? amount, long balance, long quotaUsed)
    {
        if (balance < 0)
            return "queued game balance became negative";
        if (quotaUsed is < 0 or > 100)
            return "queued game quota escaped its bounds";
        if (amount is not null && (amount <= 0 || userId is null || chatId is null))
            return "queued game contains an invalid pending bet";
        return null;
    }

    private static string? CheckDartsInvariants(DartsQueuedState state, long balance, long quotaUsed)
    {
        var genericFailure = CheckQueuedInvariants(state.Round?.UserId, state.Round?.ChatId, state.Round?.Amount, balance, quotaUsed);
        if (genericFailure is not null)
            return genericFailure;
        if (state.QueuedAhead < 0)
            return "darts queue length became negative";
        if (state.Round is { Status: DartsRoundStatus.AwaitingOutcome, BotMessageId: null })
            return "darts awaiting round has no bot message binding";
        return null;
    }

    private static IReadOnlyDictionary<string, QuotaSnapshot> Quotas(string quotaId, long used) =>
        new Dictionary<string, QuotaSnapshot> { [quotaId] = new(used, 100) };

    private static string? ProcessQueuedDecision<TState, TResult>(
        GameDecision<TState, TResult> decision,
        TState previousState,
        ref TState state,
        long previousBalance,
        ref long balance,
        long previousQuota,
        ref long quotaUsed)
    {
        if (decision.Status == DecisionStatus.Accepted)
        {
            state = decision.NewState;
            ApplyEconomyAndQuota(decision.Economy, decision.Quotas, ref balance, ref quotaUsed);
            return null;
        }

        if (!EqualityComparer<TState>.Default.Equals(decision.NewState, previousState)
            || decision.Economy.Count != 0
            || decision.Quotas.Count != 0
            || decision.CustomEffects is { Count: > 0 }
            || balance != previousBalance
            || quotaUsed != previousQuota)
            return "queued rejection mutated state or accounting";

        return null;
    }

    private static long ApplyEconomy(IReadOnlyList<EconomyEffect> effects, long balance)
    {
        foreach (var effect in effects)
            balance += effect.Kind == EconomyEffectKind.Debit ? -effect.Amount : effect.Amount;
        return balance;
    }

    private static void ApplyEconomyAndQuota(
        IReadOnlyList<EconomyEffect> economy,
        IReadOnlyList<QuotaEffect> quotas,
        ref long balance,
        ref long quotaUsed)
    {
        balance = ApplyEconomy(economy, balance);
        foreach (var effect in quotas)
            quotaUsed += effect.Kind == QuotaEffectKind.Consume ? effect.Amount : -effect.Amount;
    }

    private static void ApplyEconomy(IReadOnlyList<EconomyEffect> effects, IDictionary<long, long> balances, long defaultUser)
    {
        balances[defaultUser] = ApplyEconomy(effects, GetBalance(balances, defaultUser));
    }

    private static void ApplyCustomWalletCredits(IReadOnlyList<IGameEffect>? effects, IDictionary<long, long> balances)
    {
        foreach (var effect in effects ?? [])
        {
            if (effect is PickWalletCreditEffect credit)
                balances[credit.UserId] = GetBalance(balances, credit.UserId) + credit.Amount;
        }
    }

    private static long GetBalance(IEnumerable<KeyValuePair<long, long>> balances, long userId)
    {
        foreach (var (key, value) in balances)
        {
            if (key == userId)
                return value;
        }

        return 1_000;
    }

    private static IReadOnlyList<string> SplitCards(string deck) =>
        string.IsNullOrWhiteSpace(deck) ? [] : deck.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static long Magnitude(int value) => Math.Abs((long)value);
}
