using System.Text.Json;
using BotFramework.Sdk.Execution;
using Games.Basketball.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class BasketballActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PlaceBet_AcceptsDebitQuotaStateAndEventAsOneDecision()
    {
        var decision = Place(amount: 25, balance: 100);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(25, decision.NewState.PendingBet!.Amount);
        Assert.Equal(75, decision.Result.Balance);
        Assert.Equal([EconomyEffect.Debit(25, "basketball.bet")], decision.Economy);
        Assert.Equal([QuotaEffect.Consume(BasketballPlaceBetAction.DailyRollQuota)], decision.Quotas);
        Assert.Single(decision.Events.OfType<BasketballBetPlaced>());
    }

    [Fact]
    public void PlaceBet_RejectsInsufficientBalanceWithoutMutationEffects()
    {
        var decision = Place(amount: 25, balance: 10);

        Assert.Equal(BasketballBetError.NotEnoughCoins, decision.Result.Error);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_001)]
    public void PlaceBet_RejectsInvalidAmountWithoutMutationEffects(int amount)
    {
        var decision = Place(amount, balance: 100_000);

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(BasketballBetError.InvalidAmount, decision.Result.Error);
        Assert.Equal("invalid_amount", decision.RejectionReason);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
    }

    [Fact]
    public void PlaceBet_RejectsBlockingGameBeforeQuotaLookup()
    {
        var decision = Place(amount: 25, balance: 100, blockingGameId: "darts");

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(BasketballBetError.BusyOtherGame, decision.Result.Error);
        Assert.Equal("darts", decision.Result.BlockingGameId);
        Assert.Equal("busy_other_game", decision.RejectionReason);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
    }

    [Fact]
    public void PlaceBet_RejectsSecondBetWithoutMutationEffects()
    {
        var decision = Place(
            amount: 25,
            balance: 100,
            pending: new BasketballPendingBet(1, 10, 40, Now));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(BasketballBetError.AlreadyPending, decision.Result.Error);
        Assert.Equal(40, decision.Result.PendingAmount);
        Assert.Equal("already_pending", decision.RejectionReason);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
    }

    [Fact]
    public void PlaceBet_RequiresDailyQuotaSnapshot()
    {
        Assert.Throws<InvalidOperationException>(() => Place(amount: 25, balance: 100, includeQuota: false));
    }

    [Fact]
    public void PlaceBet_RejectsDailyLimitWithoutDebit()
    {
        var decision = Place(amount: 25, balance: 100, quotaUsed: 10, quotaLimit: 10);

        Assert.Equal(BasketballBetError.DailyRollLimit, decision.Result.Error);
        Assert.Equal(10, decision.Result.DailyRollUsed);
        Assert.Empty(decision.Economy);
    }

    [Fact]
    public void Throw_MaterializesPayoutEventsAndClearedState()
    {
        var decision = Throw(face: 4, amount: 25, entropy: 0.9, dropChance: 0.1);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Null(decision.NewState.PendingBet);
        Assert.Equal(50, decision.Result.Payout);
        Assert.Equal(150, decision.Result.Balance);
        Assert.Single(decision.Events.OfType<BasketballThrowCompleted>());
        Assert.Single(decision.Events.OfType<GameCompletedMetaEvent>());
        Assert.Empty(decision.Events.OfType<MiniGameRedeemCodeDropRequested>());
    }

    [Fact]
    public void Throw_SameInputAndEntropyProducesEqualDecision()
    {
        var first = Throw(face: 5, amount: 25, entropy: 0.01, dropChance: 0.1);
        var second = Throw(face: 5, amount: 25, entropy: 0.01, dropChance: 0.1);

        Assert.Equal(
            JsonSerializer.SerializeToUtf8Bytes(first),
            JsonSerializer.SerializeToUtf8Bytes(second));
        Assert.Single(first.Events.OfType<MiniGameRedeemCodeDropRequested>());
    }

    [Fact]
    public void Abort_RefundsStakeRestoresQuotaAndClearsState()
    {
        var action = new BasketballAbortAction();
        var decision = action.Decide(new GameActionInput<BasketballBetState, BasketballAbortCommand>(
            new BasketballAbortCommand(1, "u", 10, "abort"),
            ActiveState(30),
            new WalletSnapshot(70),
            Quotas(1, 10),
            EmptyEntropy(),
            Now));

        Assert.True(decision.Result.Aborted);
        Assert.Null(decision.NewState.PendingBet);
        Assert.Equal([EconomyEffect.Credit(30, "basketball.send_dice_failed")], decision.Economy);
        Assert.Equal([QuotaEffect.Restore(BasketballPlaceBetAction.DailyRollQuota)], decision.Quotas);
    }

    [Fact]
    public void Abort_WithoutPendingBetRejectsWithoutEffects()
    {
        var state = new BasketballBetState(null);
        var decision = new BasketballAbortAction().Decide(
            new GameActionInput<BasketballBetState, BasketballAbortCommand>(
                new BasketballAbortCommand(1, "u", 10, "abort"), state,
                new WalletSnapshot(70), Quotas(1, 10), EmptyEntropy(), Now));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.False(decision.Result.Aborted);
        Assert.Same(state, decision.NewState);
        Assert.Equal("no_pending_bet", decision.RejectionReason);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Events);
    }

    [Fact]
    public void Throw_WithoutPendingBetIsPureRejection()
    {
        var decision = Throw(face: 6, amount: 25, entropy: 0.5, dropChance: 0, hasPending: false);

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(BasketballThrowOutcome.NoBet, decision.Result.Outcome);
        Assert.Equal("no_pending_bet", decision.RejectionReason);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
        Assert.Empty(decision.Events);
    }

    [Fact]
    public void Throw_RequiresDailyQuotaSnapshotWhenBetExists()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Throw(face: 6, amount: 25, entropy: 0.5, dropChance: 0, includeQuota: false));
    }

    private static GameDecision<BasketballBetState, BasketballBetResult> Place(
        int amount,
        int balance,
        int quotaUsed = 0,
        int quotaLimit = 10,
        BasketballPendingBet? pending = null,
        string? blockingGameId = null,
        bool includeQuota = true) =>
        new BasketballPlaceBetAction().Decide(new GameActionInput<BasketballBetState, BasketballPlaceBetCommand>(
            new BasketballPlaceBetCommand(1, "u", 10, amount, "bet", 10_000, blockingGameId),
            new BasketballBetState(pending),
            new WalletSnapshot(balance),
            includeQuota ? Quotas(quotaUsed, quotaLimit) : new Dictionary<string, QuotaSnapshot>(),
            EmptyEntropy(),
            Now));

    private static GameDecision<BasketballBetState, BasketballThrowResult> Throw(
        int face,
        int amount,
        double entropy,
        double dropChance,
        bool hasPending = true,
        bool includeQuota = true) =>
        new BasketballThrowAction().Decide(new GameActionInput<BasketballBetState, BasketballThrowCommand>(
            new BasketballThrowCommand(1, "u", 10, face, "throw", dropChance),
            hasPending ? ActiveState(amount) : new BasketballBetState(null),
            new WalletSnapshot(100),
            includeQuota ? Quotas(1, 10) : new Dictionary<string, QuotaSnapshot>(),
            new EntropyValue(new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [BasketballThrowAction.RedeemDropEntropy] = entropy,
            }),
            Now));

    private static BasketballBetState ActiveState(int amount) =>
        new(new BasketballPendingBet(1, 10, amount, Now));

    private static IReadOnlyDictionary<string, QuotaSnapshot> Quotas(int used, int limit) =>
        new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
        {
            [BasketballPlaceBetAction.DailyRollQuota] = new(used, limit),
        };

    private static EntropyValue EmptyEntropy() => new(new Dictionary<string, double>());
}
