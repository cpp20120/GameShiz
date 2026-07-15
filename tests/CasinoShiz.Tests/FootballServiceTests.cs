using System.Text.Json;
using BotFramework.Sdk.Execution;
using Games.Football.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class FootballServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void PlaceBet_AcceptsDebitQuotaStateAndEventAsOneDecision()
    {
        var decision = Place(amount: 25, balance: 100);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(25, decision.NewState.PendingBet!.Amount);
        Assert.Equal(75, decision.Result.Balance);
        Assert.Equal([EconomyEffect.Debit(25, "football.bet")], decision.Economy);
        Assert.Equal([QuotaEffect.Consume(FootballPlaceBetAction.DailyRollQuota)], decision.Quotas);
        Assert.Single(decision.Events.OfType<FootballBetPlaced>());
    }

    [Fact]
    public void PlaceBet_RejectsInsufficientBalanceWithoutEffects()
    {
        var decision = Place(amount: 25, balance: 10);

        Assert.Equal(FootballBetError.NotEnoughCoins, decision.Result.Error);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
    }

    [Fact]
    public void PlaceBet_RejectsDailyLimitWithoutDebit()
    {
        var decision = Place(amount: 25, balance: 100, quotaUsed: 10, quotaLimit: 10);

        Assert.Equal(FootballBetError.DailyRollLimit, decision.Result.Error);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
    }

    [Fact]
    public void Throw_MaterializesPayoutEventsAndClearedState()
    {
        var decision = Throw(face: 5, amount: 25, entropy: 0.9, dropChance: 0.1);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Null(decision.NewState.PendingBet);
        Assert.Equal(2, decision.Result.Multiplier);
        Assert.Equal(50, decision.Result.Payout);
        Assert.Equal([EconomyEffect.Credit(50, "football.payout")], decision.Economy);
        Assert.Single(decision.Events.OfType<FootballThrowCompleted>());
        Assert.Single(decision.Events.OfType<GameCompletedMetaEvent>());
    }

    [Fact]
    public void Throw_SameInputAndEntropyProducesEqualDecision()
    {
        var first = Throw(face: 4, amount: 25, entropy: 0.01, dropChance: 0.1);
        var second = Throw(face: 4, amount: 25, entropy: 0.01, dropChance: 0.1);

        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(first), JsonSerializer.SerializeToUtf8Bytes(second));
        Assert.Single(first.Events.OfType<MiniGameRedeemCodeDropRequested>());
    }

    [Fact]
    public void Throw_WithoutBetRejectsWithoutEffects()
    {
        var decision = new FootballThrowAction().Decide(Input(
            new FootballThrowCommand(1, "u", 10, 5, "throw", 0),
            new FootballBetState(null), 100, 0, 10, 0.5));

        Assert.Equal(FootballThrowOutcome.NoBet, decision.Result.Outcome);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Events);
    }

    [Fact]
    public void Abort_RefundsStakeRestoresQuotaAndClearsState()
    {
        var decision = new FootballAbortAction().Decide(Input(
            new FootballAbortCommand(1, "u", 10, "abort"), ActiveState(30), 70, 1, 10));

        Assert.True(decision.Result.Aborted);
        Assert.Null(decision.NewState.PendingBet);
        Assert.Equal([EconomyEffect.Credit(30, "football.send_dice_failed")], decision.Economy);
        Assert.Equal([QuotaEffect.Restore(FootballPlaceBetAction.DailyRollQuota)], decision.Quotas);
        Assert.Single(decision.Events.OfType<FootballBetAborted>());
    }

    private static GameDecision<FootballBetState, FootballBetResult> Place(
        int amount, int balance, int quotaUsed = 0, int quotaLimit = 10) =>
        new FootballPlaceBetAction().Decide(Input(
            new FootballPlaceBetCommand(1, "u", 10, amount, "bet", 10_000, null),
            new FootballBetState(null), balance, quotaUsed, quotaLimit));

    private static GameDecision<FootballBetState, FootballThrowResult> Throw(
        int face, int amount, double entropy, double dropChance) =>
        new FootballThrowAction().Decide(Input(
            new FootballThrowCommand(1, "u", 10, face, "throw", dropChance),
            ActiveState(amount), 100, 1, 10, entropy));

    private static GameActionInput<FootballBetState, TCommand> Input<TCommand>(
        TCommand command, FootballBetState state, int balance, int used, int limit, double entropy = 0.5) =>
        new(command, state, new WalletSnapshot(balance),
            new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
            {
                [FootballPlaceBetAction.DailyRollQuota] = new(used, limit),
            },
            new EntropyValue(new Dictionary<string, double>(StringComparer.Ordinal)
            {
                [FootballThrowAction.RedeemDropEntropy] = entropy,
            }), Now);

    private static FootballBetState ActiveState(int amount) =>
        new(new FootballPendingBet(1, 10, amount, Now));
}
