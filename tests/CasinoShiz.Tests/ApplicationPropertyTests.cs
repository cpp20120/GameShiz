using BotFramework.Sdk.Execution;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Games.Basketball.Application.Execution;
using Games.Blackjack.Application.Execution;
using Games.Darts.Application.Execution;
using Games.Dice.Application.Execution;
using Games.DiceCube.Application.Execution;
using Games.Poker.Application.Execution;

namespace CasinoShiz.Tests;

public sealed class ApplicationPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);

    [Property(MaxTest = 100)]
    public Property Dice_ValidTelegramValueProducesBalancedAcceptedDecision(PositiveInt seed)
    {
        var diceValue = 1 + seed.Get % 64;
        var decision = DecideDice(diceValue);

        var isValid = decision.Status == DecisionStatus.Accepted
            && decision.Result.Outcome == DiceOutcome.Played
            && decision.Result.NewBalance
                == 100_000 - decision.Result.Loss + decision.Result.Prize
            && decision.Result.Loss == 8
            && decision.Result.DailyDiceUsed == 1
            && decision.Result.DailyDiceLimit == 10
            && decision.Economy.SequenceEqual(ExpectedDiceEconomy(decision.Result))
            && decision.Quotas.SequenceEqual(
                [QuotaEffect.Consume(DiceAction.DailyRollQuota)]);

        return isValid
            .ToProperty()
            .Label($"diceValue={diceValue}, decision={decision}");
    }

    [Property(MaxTest = 100)]
    public Property Basketball_PlaceThenAbortReturnsStakeAndQuota(PositiveInt seed)
    {
        var amount = 1 + seed.Get % 10_000;
        var balance = amount + seed.Get % 10_000;
        var placed = PlaceBasketball(amount, balance);

        if (placed.Status != DecisionStatus.Accepted)
        {
            return false
                .ToProperty()
                .Label($"place rejected for amount={amount}, balance={balance}: {placed}");
        }

        var aborted = new BasketballAbortAction().Decide(
            new GameActionInput<BasketballBetState, BasketballAbortCommand>(
                new BasketballAbortCommand(1, "u", 10, "abort"),
                placed.NewState,
                new WalletSnapshot(placed.Result.Balance),
                Quotas(used: 1, limit: 10),
                EntropyValue.Empty,
                Now));

        var isValid = aborted.Status == DecisionStatus.Accepted
            && aborted.Result.Aborted
            && aborted.NewState.PendingBet is null
            && placed.Result.Balance + amount == balance
            && placed.Economy.SequenceEqual(
                [EconomyEffect.Debit(amount, "basketball.bet")])
            && placed.Quotas.SequenceEqual(
                [QuotaEffect.Consume(BasketballPlaceBetAction.DailyRollQuota)])
            && aborted.Economy.SequenceEqual(
                [EconomyEffect.Credit(amount, "basketball.send_dice_failed")])
            && aborted.Quotas.SequenceEqual(
                [QuotaEffect.Restore(BasketballPlaceBetAction.DailyRollQuota)]);

        return isValid
            .ToProperty()
            .Label($"amount={amount}, balance={balance}, placed={placed}, aborted={aborted}");
    }

    [Property(MaxTest = 100)]
    public Property Dice_SameInputAndEntropyProducesTheSameDecision(NonNegativeInt seed)
    {
        var diceValue = 1 + seed.Get % 64;
        var entropy = (seed.Get % 99) / 100.0;
        var first = DecideDice(diceValue, entropy);
        var second = DecideDice(diceValue, entropy);

        var isEqual = first.Status == second.Status
            && first.NewState == second.NewState
            && first.Result == second.Result
            && first.Economy.SequenceEqual(second.Economy)
            && first.Quotas.SequenceEqual(second.Quotas)
            && first.Records.SequenceEqual(second.Records)
            && first.Events.SequenceEqual(second.Events)
            && first.Schedules.SequenceEqual(second.Schedules)
            && first.RejectionReason == second.RejectionReason;

        return isEqual
            .ToProperty()
            .Label($"diceValue={diceValue}, entropy={entropy}");
    }

    [Property(MaxTest = 100)]
    public Property Darts_ValidInputConservesBalanceAndEmitsMatchingEffects(PositiveInt seed)
    {
        var face = seed.Get % 100;
        var amount = 1 + seed.Get % 10_000;
        var balance = amount + seed.Get % 10_000;
        var decision = DecideDarts(face, amount, balance);
        var multiplier = face is 4 ? 1 : face is 5 or 6 ? 2 : 0;
        var payout = amount * multiplier;

        var isValid = decision.Status == DecisionStatus.Accepted
            && decision.Result.Outcome == DartsThrowOutcome.Thrown
            && decision.Result.Multiplier == multiplier
            && decision.Result.Payout == payout
            && decision.Result.Balance == balance - amount + payout
            && decision.Result.DailyRollUsed == 1
            && decision.Result.DailyRollLimit == 10
            && decision.Economy.SequenceEqual(ExpectedDartsEconomy(amount, payout))
            && decision.Quotas.SequenceEqual(
                [QuotaEffect.Consume(DartsQuickThrowAction.DailyRollQuota)]);

        return isValid
            .ToProperty()
            .Label($"face={face}, amount={amount}, balance={balance}, decision={decision}");
    }

    [Property(MaxTest = 100)]
    public Property DiceCube_PlaceThenRollUsesSnapshottedMultipliers(PositiveInt seed)
    {
        var amount = 1 + seed.Get % 1_000;
        var balance = amount + seed.Get % 10_000;
        var face = 1 + seed.Get % 6;
        var mult4 = seed.Get % 11;
        var mult5 = (seed.Get / 11) % 11;
        var mult6 = (seed.Get / 121) % 11;

        var placed = new DiceCubePlaceBetAction().Decide(
            new GameActionInput<DiceCubePlaceBetState, DiceCubePlaceBetCommand>(
                new DiceCubePlaceBetCommand(
                    1, "player", 100, amount, "dicecube:pbt", 10_000,
                    mult4, mult5, mult6, 0, null),
                new DiceCubePlaceBetState(null),
                new WalletSnapshot(balance),
                DiceCubeQuotas(used: 0, limit: 10),
                EntropyValue.Empty,
                Now));

        if (placed.Status != DecisionStatus.Accepted)
        {
            return false
                .ToProperty()
                .Label($"place rejected for amount={amount}, balance={balance}: {placed}");
        }

        var roll = new DiceCubeRollAction().Decide(
            new GameActionInput<DiceCubePlaceBetState, DiceCubeRollCommand>(
                new DiceCubeRollCommand(1, "player", 100, face, "dicecube:roll", 0),
                placed.NewState,
                new WalletSnapshot(placed.Result.Balance),
                DiceCubeQuotas(used: 1, limit: 10),
                EntropyValue.Empty,
                Now));

        var multiplier = face switch
        {
            4 => mult4,
            5 => mult5,
            6 => mult6,
            _ => 0,
        };
        var payout = amount * multiplier;
        var isValid = roll.Status == DecisionStatus.Accepted
            && roll.Result.Outcome == CubeRollOutcome.Rolled
            && roll.Result.Multiplier == multiplier
            && roll.Result.Payout == payout
            && roll.Result.Balance == balance - amount + payout
            && roll.NewState.PendingBet is null
            && roll.Economy.SequenceEqual(ExpectedCubeEconomy(payout));

        return isValid
            .ToProperty()
            .Label($"face={face}, amount={amount}, multipliers={mult4}/{mult5}/{mult6}, roll={roll}");
    }

    [Property(MaxTest = 100)]
    public Property Blackjack_ValidStartProducesConsistentAcceptedDecision(PositiveInt seed)
    {
        var bet = 1 + seed.Get % 100;
        var balance = bet + 1_000;
        var entropy = (seed.Get % 99) / 100.0;
        var decision = DecideBlackjackStart(bet, balance, entropy);
        var snapshot = decision.Result.Snapshot;
        var netBalance = balance
            - decision.Economy
                .Where(effect => effect.Kind == EconomyEffectKind.Debit)
                .Sum(effect => effect.Amount)
            + decision.Economy
                .Where(effect => effect.Kind == EconomyEffectKind.Credit)
                .Sum(effect => effect.Amount);
        var activeShape = decision.NewState.Status == TurnGameStatus.Active
            && decision.NewState.Hand is not null
            && decision.Schedules.Count == 1
            && decision.Events.Count == 1;
        var naturalShape = decision.NewState.Status == TurnGameStatus.Completed
            && decision.NewState.Hand is null
            && decision.Schedules.Count == 0
            && decision.Events.Count == 3;

        var isValid = decision.Status == DecisionStatus.Accepted
            && decision.Result.Error == BlackjackError.None
            && decision.NewState.Revision == 1
            && decision.Economy.Count >= 1
            && decision.Economy[0] == EconomyEffect.Debit(bet, "blackjack.start")
            && snapshot is not null
            && snapshot.PlayerCoins == netBalance
            && (activeShape || naturalShape);

        return isValid
            .ToProperty()
            .Label($"bet={bet}, entropy={entropy}, decision={decision}");
    }

    [Property(MaxTest = 100)]
    public Property Poker_InviteCodeIsDeterministicAndProtocolSafe(NonNegativeInt seed)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var entropy = (seed.Get % 33_554_432) / 33_554_432d;
        var first = PokerExecutionRules.InviteCode(entropy);
        var second = PokerExecutionRules.InviteCode(entropy);

        var isValid = first == second
            && first.Length == 5
            && first.All(alphabet.Contains);

        return isValid
            .ToProperty()
            .Label($"entropy={entropy}, code={first}");
    }

    private static IReadOnlyList<EconomyEffect> ExpectedDiceEconomy(DicePlayResult result) =>
        result.Prize > 0
            ? [EconomyEffect.Debit(result.Loss, "dice.stake"), EconomyEffect.Credit(result.Prize, "dice.prize")]
            : [EconomyEffect.Debit(result.Loss, "dice.stake")];

    private static IReadOnlyList<EconomyEffect> ExpectedDartsEconomy(int amount, int payout) =>
        payout > 0
            ? [EconomyEffect.Debit(amount, "darts.quickplay.bet"), EconomyEffect.Credit(payout, "darts.quickplay.payout")]
            : [EconomyEffect.Debit(amount, "darts.quickplay.bet")];

    private static IReadOnlyList<EconomyEffect> ExpectedCubeEconomy(int payout) =>
        payout > 0 ? [EconomyEffect.Credit(payout, "dicecube.payout")] : [];

    private static GameDecision<BlackjackGameState, BlackjackResult> DecideBlackjackStart(
        int bet,
        long balance,
        double entropy) =>
        new BlackjackStartAction().Decide(
            new GameActionInput<BlackjackGameState, BlackjackStartCommand>(
                new BlackjackStartCommand(1, "player", 10, bet, "blackjack:pbt", 1, 100, 60_000),
                new BlackjackGameState(0, TurnGameStatus.Completed, 1, null, "player", null),
                new WalletSnapshot(balance),
                new Dictionary<string, QuotaSnapshot>(),
                new EntropyValue(
                    Enumerable.Range(1, 51)
                        .Select(index => KeyValuePair.Create($"shuffle-{index}", entropy))),
                Now));

    private static GameDecision<NoGameState, DicePlayResult> DecideDice(
        int diceValue,
        double entropy = 0.5) =>
        new DiceAction().Decide(new GameActionInput<NoGameState, DiceCommand>(
            new DiceCommand(1, "player", diceValue, 100, diceValue, false, 7, 0),
            default,
            new WalletSnapshot(100_000),
            new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
            {
                [DiceAction.DailyRollQuota] = new(0, 10),
            },
            new EntropyValue([KeyValuePair.Create(DiceAction.RedeemDropEntropy, entropy)]),
            Now));

    private static GameDecision<BasketballBetState, BasketballBetResult> PlaceBasketball(
        int amount,
        int balance) =>
        new BasketballPlaceBetAction().Decide(
            new GameActionInput<BasketballBetState, BasketballPlaceBetCommand>(
                new BasketballPlaceBetCommand(1, "u", 10, amount, "bet", 10_000, null),
                new BasketballBetState(null),
                new WalletSnapshot(balance),
                Quotas(used: 0, limit: 10),
                EntropyValue.Empty,
                Now));

    private static IReadOnlyDictionary<string, QuotaSnapshot> Quotas(int used, int limit) =>
        new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
        {
            [BasketballPlaceBetAction.DailyRollQuota] = new(used, limit),
        };

    private static GameDecision<NoGameState, DartsThrowResult> DecideDarts(
        int face,
        int amount,
        long balance) =>
        new DartsQuickThrowAction().Decide(
            new GameActionInput<NoGameState, DartsQuickThrowCommand>(
                new DartsQuickThrowCommand(1, "player", 100, 10, face, amount, 10_000, 0, null),
                default,
                new WalletSnapshot(balance),
                new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
                {
                    [DartsQuickThrowAction.DailyRollQuota] = new(0, 10),
                },
                EntropyValue.Empty,
                Now));

    private static IReadOnlyDictionary<string, QuotaSnapshot> DiceCubeQuotas(int used, int limit) =>
        new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
        {
            [DiceCubePlaceBetAction.DailyRollQuota] = new(used, limit),
        };
}
