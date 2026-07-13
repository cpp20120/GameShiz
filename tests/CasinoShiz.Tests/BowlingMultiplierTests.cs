using System.Text.Json;
using BotFramework.Sdk.Execution;
using Games.Bowling.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class BowlingMultiplierTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(4, 1)]
    [InlineData(5, 2)]
    [InlineData(6, 2)]
    public void Multipliers_CorrectByFace(int face, int expected) =>
        Assert.Equal(expected, BowlingService.Multipliers[face]);

    [Fact]
    public void PlaceBet_AcceptsDebitQuotaStateAndEventAsOneDecision()
    {
        var decision = Place(amount: 50, balance: 100);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(50, decision.NewState.PendingBet!.Amount);
        Assert.Equal([EconomyEffect.Debit(50, "bowling.bet")], decision.Economy);
        Assert.Equal([QuotaEffect.Consume(BowlingPlaceBetAction.DailyRollQuota)], decision.Quotas);
        Assert.Single(decision.Events.OfType<BowlingBetPlaced>());
    }

    [Fact]
    public void PlaceBet_RejectsBalanceAndQuotaWithoutMutationEffects()
    {
        var balance = Place(amount: 50, balance: 10);
        var quota = Place(amount: 50, balance: 100, used: 10, limit: 10);

        Assert.Equal(BowlingBetError.NotEnoughCoins, balance.Result.Error);
        Assert.Equal(BowlingBetError.DailyRollLimit, quota.Result.Error);
        Assert.Empty(balance.Economy);
        Assert.Empty(quota.Economy);
        Assert.Empty(quota.Quotas);
    }

    [Theory]
    [InlineData(4, 1, 50)]
    [InlineData(6, 2, 100)]
    public void Roll_CreditsExpectedPayoutAndClearsState(int face, int multiplier, int payout)
    {
        var decision = Roll(face, amount: 50, entropy: 0.9, dropChance: 0.1);

        Assert.Equal(BowlingRollOutcome.Rolled, decision.Result.Outcome);
        Assert.Equal(multiplier, decision.Result.Multiplier);
        Assert.Equal(payout, decision.Result.Payout);
        Assert.Null(decision.NewState.PendingBet);
        Assert.Equal([EconomyEffect.Credit(payout, "bowling.payout")], decision.Economy);
        Assert.Single(decision.Events.OfType<BowlingRollCompleted>());
        Assert.Single(decision.Events.OfType<GameCompletedMetaEvent>());
    }

    [Fact]
    public void Roll_SameInputAndEntropyProducesEqualDecision()
    {
        var first = Roll(6, 50, 0.01, 0.1);
        var second = Roll(6, 50, 0.01, 0.1);

        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(first), JsonSerializer.SerializeToUtf8Bytes(second));
        Assert.Single(first.Events.OfType<TelegramMiniGameRedeemCodeDropRequested>());
    }

    [Fact]
    public void Abort_RefundsStakeRestoresQuotaAndClearsState()
    {
        var decision = new BowlingAbortAction().Decide(Input(
            new BowlingAbortCommand(1, "u", 10, "abort"), ActiveState(30), 70, 1, 10));

        Assert.True(decision.Result.Aborted);
        Assert.Null(decision.NewState.PendingBet);
        Assert.Equal([EconomyEffect.Credit(30, "bowling.send_dice_failed")], decision.Economy);
        Assert.Equal([QuotaEffect.Restore(BowlingPlaceBetAction.DailyRollQuota)], decision.Quotas);
        Assert.Single(decision.Events.OfType<BowlingBetAborted>());
    }

    private static GameDecision<BowlingBetState, BowlingBetResult> Place(
        int amount, int balance, int used = 0, int limit = 10) =>
        new BowlingPlaceBetAction().Decide(Input(
            new BowlingPlaceBetCommand(1, "u", 10, amount, "bet", 10_000, null),
            new BowlingBetState(null), balance, used, limit));

    private static GameDecision<BowlingBetState, BowlingRollResult> Roll(
        int face, int amount, double entropy, double dropChance) =>
        new BowlingRollAction().Decide(Input(
            new BowlingRollCommand(1, "u", 10, face, "roll", dropChance),
            ActiveState(amount), 100, 1, 10, entropy));

    private static GameActionInput<BowlingBetState, TCommand> Input<TCommand>(
        TCommand command, BowlingBetState state, int balance, int used, int limit, double entropy = 0.5) =>
        new(command, state, new WalletSnapshot(balance),
            new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
            {
                [BowlingPlaceBetAction.DailyRollQuota] = new(used, limit),
            },
            new EntropyValue(new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [BowlingRollAction.RedeemDropEntropy] = entropy,
            }), Now);

    private static BowlingBetState ActiveState(int amount) =>
        new(new BowlingPendingBet(1, 10, amount, Now));
}
