using BotFramework.Sdk.Execution;
using Games.DiceCube.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class DiceCubeSettlementActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 10, 0, 0, TimeSpan.Zero);
    private static readonly DiceCubePendingBet Bet = new(1, 100, 50, Now.AddSeconds(-1), 1, 2, 2);

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(4, 1, 50)]
    [InlineData(5, 2, 100)]
    [InlineData(6, 2, 100)]
    [InlineData(99, 0, 0)]
    public void RollDecide_UsesSnapshottedRulesAndClearsPendingState(
        int face,
        int expectedMultiplier,
        int expectedPayout)
    {
        var decision = Roll(face);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(CubeRollOutcome.Rolled, decision.Result.Outcome);
        Assert.Equal(expectedMultiplier, decision.Result.Multiplier);
        Assert.Equal(expectedPayout, decision.Result.Payout);
        Assert.Null(decision.NewState.PendingBet);
        Assert.Equal(expectedPayout > 0 ? 1 : 0, decision.Economy.Count);
        Assert.Empty(decision.Quotas);
        Assert.Collection(
            decision.Events,
            item => Assert.IsType<DiceCubeRollCompleted>(item),
            item => Assert.IsType<GameCompletedMetaEvent>(item));
    }

    [Fact]
    public void RollDecide_NoBetIsPureRejection()
    {
        var decision = Roll(4, hasPending: false);

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(CubeRollOutcome.NoBet, decision.Result.Outcome);
        Assert.Equal("no_pending_bet", decision.RejectionReason);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Events);
    }

    [Fact]
    public void RollDecide_RedeemDropUsesProvidedEntropy()
    {
        var decision = Roll(6, redeemDropChance: 0.1, entropy: 0.01);

        Assert.IsType<TelegramMiniGameRedeemCodeDropRequested>(decision.Events[2]);
    }

    [Fact]
    public void AbortDecide_RefundsStakeRestoresQuotaAndClearsState()
    {
        var decision = Abort(Bet);

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.True(decision.Result.Aborted);
        Assert.Null(decision.NewState.PendingBet);
        var credit = Assert.Single(decision.Economy);
        Assert.Equal(EconomyEffectKind.Credit, credit.Kind);
        Assert.Equal(50, credit.Amount);
        Assert.Equal("dicecube.bot_dice.failed", credit.Reason);
        Assert.Equal(QuotaEffectKind.Restore, Assert.Single(decision.Quotas).Kind);
        Assert.IsType<DiceCubeBetAborted>(Assert.Single(decision.Events));
    }

    [Fact]
    public void AbortDecide_NoBetHasNoEffects()
    {
        var decision = Abort(null);

        Assert.False(decision.Result.Aborted);
        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
    }

    private static GameDecision<DiceCubePlaceBetState, CubeRollResult> Roll(
        int face,
        bool hasPending = true,
        double redeemDropChance = 0,
        double entropy = 0.5) =>
        new DiceCubeRollAction().Decide(
            new GameActionInput<DiceCubePlaceBetState, DiceCubeRollCommand>(
                new DiceCubeRollCommand(1, "player", 100, face, "roll", redeemDropChance),
                new DiceCubePlaceBetState(hasPending ? Bet : null),
                new WalletSnapshot(50),
                Quotas(),
                new EntropyValue([KeyValuePair.Create(DiceCubeRollAction.RedeemDropEntropy, entropy)]),
                Now));

    private static GameDecision<DiceCubePlaceBetState, DiceCubeAbortResult> Abort(DiceCubePendingBet? pending) =>
        new DiceCubeAbortAction().Decide(
            new GameActionInput<DiceCubePlaceBetState, DiceCubeAbortCommand>(
                new DiceCubeAbortCommand(1, "player", 100, "abort"),
                new DiceCubePlaceBetState(pending),
                new WalletSnapshot(50),
                Quotas(),
                new EntropyValue([]),
                Now));

    private static IReadOnlyDictionary<string, QuotaSnapshot> Quotas() =>
        new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
        {
            [DiceCubePlaceBetAction.DailyRollQuota] = new(1, 10),
        };
}
