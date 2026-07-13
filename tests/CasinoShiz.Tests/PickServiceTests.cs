using System.Text.Json;
using BotFramework.Sdk.Execution;
using Games.Pick.Application.Execution;
using Games.Pick.Domain.Events;
using Games.Pick.Domain.Results;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PickServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, PickError.InvalidAmount)]
    [InlineData(101, PickError.InvalidAmount)]
    public void Decide_InvalidAmountRejectsWithoutEffects(int amount, PickError error)
    {
        var decision = Decide(Command(amount: amount), balance: 100, entropy: 0);

        Assert.Equal(error, decision.Result.Error);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.CustomEffects ?? []);
    }

    [Fact]
    public void Decide_InsufficientBalanceRejectsWithoutDebit()
    {
        var decision = Decide(Command(amount: 10), balance: 9, entropy: 0);

        Assert.Equal(PickError.NotEnoughCoins, decision.Result.Error);
        Assert.Equal(9, decision.Result.Balance);
        Assert.Empty(decision.Economy);
    }

    [Fact]
    public void Decide_WinProducesAtomicDebitCreditStateEventsAndChainEffect()
    {
        var decision = Decide(Command(), balance: 100, entropy: 0, streak: 1);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.True(decision.Result.Won);
        Assert.Equal(2, decision.NewState.Streak);
        Assert.Equal(24, decision.Result.Payout);
        Assert.Equal(114, decision.Result.Balance);
        Assert.Equal(
            [EconomyEffect.Debit(10, "pick.bet"), EconomyEffect.Credit(24, "pick.win")],
            decision.Economy);
        Assert.Single(decision.Events.OfType<PickPlayed>());
        Assert.Single(decision.Events.OfType<GameCompletedMetaEvent>());
        Assert.Single((decision.CustomEffects ?? []).OfType<PickChainOfferEffect>());
    }

    [Fact]
    public void Decide_LossResetsTopLevelStreak()
    {
        var decision = Decide(Command(), balance: 100, entropy: 0.75, streak: 4);

        Assert.False(decision.Result.Won);
        Assert.Equal(0, decision.NewState.Streak);
        Assert.Equal([EconomyEffect.Debit(10, "pick.bet")], decision.Economy);
    }

    [Fact]
    public void Decide_ChainDoesNotChangeStreakAndUsesChainReasons()
    {
        var decision = Decide(Command(depth: 1, applyStreak: false), balance: 100, entropy: 0, streak: 3);

        Assert.Equal(3, decision.NewState.Streak);
        Assert.Equal(19, decision.Result.Payout);
        Assert.Equal(
            [EconomyEffect.Debit(10, "pick.chain.bet"), EconomyEffect.Credit(19, "pick.chain.win")],
            decision.Economy);
    }

    [Fact]
    public void Decide_SameInputAndEntropyProducesBitwiseEqualDecisionAndChainId()
    {
        var first = Decide(Command(), 100, 0, 1);
        var second = Decide(Command(), 100, 0, 1);

        Assert.Equal(JsonSerializer.SerializeToUtf8Bytes(first), JsonSerializer.SerializeToUtf8Bytes(second));
        Assert.Equal(first.Result.ChainGuid, second.Result.ChainGuid);
    }

    private static GameDecision<PickGameState, PickResult> Decide(
        PickCommand command, int balance, double entropy, int streak = 0) =>
        new PickAction().Decide(new(
            command,
            new PickGameState(streak),
            new WalletSnapshot(balance),
            new Dictionary<string, QuotaSnapshot>(),
            new EntropyValue(new Dictionary<string, double>
            {
                [PickAction.OutcomeEntropy] = entropy,
            }),
            Now));

    private static PickCommand Command(int amount = 10, int depth = 0, bool applyStreak = true) =>
        new(1, "u", 100, amount, ["a", "b"], [0], depth, applyStreak, "pick-command",
            2, 4, 100, 0.05, 0.5, 4, 5, 60);
}
