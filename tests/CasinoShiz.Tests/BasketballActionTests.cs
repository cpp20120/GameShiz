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

    private static GameDecision<BasketballBetState, BasketballBetResult> Place(
        int amount,
        int balance,
        int quotaUsed = 0,
        int quotaLimit = 10) =>
        new BasketballPlaceBetAction().Decide(new GameActionInput<BasketballBetState, BasketballPlaceBetCommand>(
            new BasketballPlaceBetCommand(1, "u", 10, amount, "bet", 10_000, null),
            new BasketballBetState(null),
            new WalletSnapshot(balance),
            Quotas(quotaUsed, quotaLimit),
            EmptyEntropy(),
            Now));

    private static GameDecision<BasketballBetState, BasketballThrowResult> Throw(
        int face,
        int amount,
        double entropy,
        double dropChance) =>
        new BasketballThrowAction().Decide(new GameActionInput<BasketballBetState, BasketballThrowCommand>(
            new BasketballThrowCommand(1, "u", 10, face, "throw", dropChance),
            ActiveState(amount),
            new WalletSnapshot(100),
            Quotas(1, 10),
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
