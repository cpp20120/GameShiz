using BotFramework.Sdk.Execution;
using Games.Darts.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class DartsQuickThrowActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 12, 5, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(4, 1, 10)]
    [InlineData(5, 2, 20)]
    [InlineData(6, 2, 20)]
    [InlineData(99, 0, 0)]
    public void Decide_SettlesDebitAndPayoutAtomically(int face, int multiplier, int payout)
    {
        var decision = Decide(face: face);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(DartsThrowOutcome.Thrown, decision.Result.Outcome);
        Assert.Equal(multiplier, decision.Result.Multiplier);
        Assert.Equal(payout, decision.Result.Payout);
        Assert.Equal(90 + payout, decision.Result.Balance);
        Assert.Equal(payout > 0 ? 2 : 1, decision.Economy.Count);
        Assert.Single(decision.Quotas);
        Assert.Collection(
            decision.Events,
            item => Assert.IsType<DartsThrowCompleted>(item),
            item => Assert.IsType<GameCompletedMetaEvent>(item));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Decide_InvalidAmountRejectsWithoutEffects(int amount)
    {
        var decision = Decide(amount: amount, maxBet: 100);

        Assert.Equal(DartsThrowOutcome.BetInvalid, decision.Result.Outcome);
        Assert.Equal("invalid_amount", decision.RejectionReason);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
    }

    [Fact]
    public void Decide_BalanceAndQuotaRejectionsDoNotPartiallyMutate()
    {
        var balance = Decide(balance: 9);
        var quota = Decide(quotaUsed: 10, quotaLimit: 10);

        Assert.Equal(DartsThrowOutcome.BetNotEnoughCoins, balance.Result.Outcome);
        Assert.Equal(DartsThrowOutcome.BetDailyLimit, quota.Result.Outcome);
        Assert.Empty(balance.Economy);
        Assert.Empty(quota.Economy);
    }

    [Fact]
    public void Decide_RedeemDropUsesFrameworkEntropy()
    {
        var decision = Decide(redeemChance: 0.1, entropy: 0.01);

        Assert.IsType<TelegramMiniGameRedeemCodeDropRequested>(decision.Events[2]);
    }

    private static GameDecision<NoGameState, DartsThrowResult> Decide(
        int face = 4,
        int amount = 10,
        int maxBet = 100,
        long balance = 100,
        long quotaUsed = 0,
        long quotaLimit = 10,
        double redeemChance = 0,
        double entropy = 0.5) =>
        new DartsQuickThrowAction().Decide(
            new GameActionInput<NoGameState, DartsQuickThrowCommand>(
                new DartsQuickThrowCommand(1, "player", 100, 10, face, amount, maxBet, redeemChance, null),
                default,
                new WalletSnapshot(balance),
                new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
                {
                    [DartsQuickThrowAction.DailyRollQuota] = new(quotaUsed, quotaLimit),
                },
                new EntropyValue([KeyValuePair.Create(DartsQuickThrowAction.RedeemDropEntropy, entropy)]),
                Now));
}
