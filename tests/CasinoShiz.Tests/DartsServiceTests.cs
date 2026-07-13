using BotFramework.Sdk.Execution;
using Games.Darts.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class DartsServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void PlaceBet_InvalidAmount_HasNoEffects(int amount)
    {
        var decision = new DartsPlaceBetAction().Decide(Input(
            Place(amount), new DartsQueuedState(null, 0), 100));
        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(DartsBetError.InvalidAmount, decision.Result.Error);
        Assert.Empty(decision.Economy);
    }

    [Fact]
    public void PlaceBet_QuotaExceeded_HasNoDebit()
    {
        var decision = new DartsPlaceBetAction().Decide(Input(
            Place(10), new DartsQueuedState(null, 0), 100, used: 3, limit: 3));
        Assert.Equal(DartsBetError.DailyRollLimit, decision.Result.Error);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
    }

    [Fact]
    public void PlaceBet_AcceptsDebitQuotaRoundAndEventTogether()
    {
        var decision = new DartsPlaceBetAction().Decide(Input(
            Place(25), new DartsQueuedState(null, 2), 100));
        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(75, decision.Result.Balance);
        Assert.Equal(2, decision.Result.QueuedAhead);
        Assert.Equal(25, Assert.Single(decision.Economy).Amount);
        Assert.Equal(QuotaEffectKind.Consume, Assert.Single(decision.Quotas).Kind);
        Assert.NotNull(decision.NewState.Round);
        Assert.IsType<DartsBetPlaced>(Assert.Single(decision.Events));
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(4, 1, 50)]
    [InlineData(5, 2, 100)]
    [InlineData(6, 2, 100)]
    public void Resolve_UsesLockedRoundAndProducesPayout(int face, int multiplier, int payout)
    {
        var round = Round(DartsRoundStatus.AwaitingOutcome, botMessageId: 9001);
        var command = new DartsResolveRoundCommand(7, 1, "u", 10, 9001, face, "resolve", 0);
        var decision = new DartsResolveRoundAction().Decide(Input(
            command, new DartsQueuedState(round, 0), 50));
        Assert.Equal(DartsThrowOutcome.Thrown, decision.Result.Outcome);
        Assert.Equal(multiplier, decision.Result.Multiplier);
        Assert.Equal(payout, decision.Result.Payout);
        Assert.Null(decision.NewState.Round);
    }

    [Fact]
    public void Abort_RefundsWalletAndQuotaInSameDecision()
    {
        var round = Round(DartsRoundStatus.Queued);
        var command = new DartsAbortRoundCommand(7, 1, "u", 10, "abort");
        var decision = new DartsAbortRoundAction().Decide(Input(
            command, new DartsQueuedState(round, 0), 50));
        Assert.True(decision.Result.Aborted);
        Assert.Equal(EconomyEffectKind.Credit, Assert.Single(decision.Economy).Kind);
        Assert.Equal(QuotaEffectKind.Restore, Assert.Single(decision.Quotas).Kind);
        Assert.IsType<DartsBetAborted>(Assert.Single(decision.Events));
    }

    [Fact]
    public void Bullseye_MatchesDiceCubeDefaultMultiplier() =>
        Assert.Equal(DiceCubeService.BuildMultipliers(new DiceCubeOptions())[6], DartsRules.Multiplier(6));

    private static DartsPlaceBetCommand Place(int amount) =>
        new(1, "u", 10, amount, 100, 7, "place", 100, null);

    private static DartsRound Round(DartsRoundStatus status, int? botMessageId = null) =>
        new(7, 1, 10, 50, Now, status, botMessageId, 100);

    private static GameActionInput<DartsQueuedState, TCommand> Input<TCommand>(
        TCommand command, DartsQueuedState state, long balance, long used = 0, long limit = 10) =>
        new(command, state, new WalletSnapshot(balance),
            new Dictionary<string, QuotaSnapshot>
            {
                [DartsPlaceBetAction.DailyRollQuota] = new(used, limit),
            },
            new EntropyValue(new Dictionary<string, double>
            {
                [DartsResolveRoundAction.RedeemDropEntropy] = 0.999999,
            }), Now);
}
