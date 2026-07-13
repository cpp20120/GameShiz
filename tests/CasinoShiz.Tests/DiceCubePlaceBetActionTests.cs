using BotFramework.Sdk.Execution;
using Games.DiceCube.Application.Execution;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class DiceCubePlaceBetActionTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 11, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Decide_AcceptedBetCreatesAtomicDebitQuotaAndPendingState()
    {
        var decision = Decide();

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(CubeBetError.None, decision.Result.Error);
        Assert.Equal(50, decision.Result.Amount);
        Assert.Equal(50, decision.Result.Balance);
        var debit = Assert.Single(decision.Economy);
        Assert.Equal(EconomyEffectKind.Debit, debit.Kind);
        Assert.Equal(50, debit.Amount);
        Assert.Equal("dicecube.bet", debit.Reason);
        Assert.Equal(DiceCubePlaceBetAction.DailyRollQuota, Assert.Single(decision.Quotas).QuotaId);
        Assert.Equal(50, Assert.IsType<DiceCubePendingBet>(decision.NewState.PendingBet).Amount);
        Assert.IsType<DiceCubeBetPlaced>(Assert.Single(decision.Events));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public void Decide_InvalidAmountHasNoMutationEffects(int amount)
    {
        var decision = Decide(amount: amount, maxBet: 100);

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(CubeBetError.InvalidAmount, decision.Result.Error);
        Assert.Equal("invalid_amount", decision.RejectionReason);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
    }

    [Fact]
    public void Decide_InsufficientBalanceDoesNotConsumeQuota()
    {
        var decision = Decide(balance: 10);

        Assert.Equal(CubeBetError.NotEnoughCoins, decision.Result.Error);
        Assert.Equal("insufficient_balance", decision.RejectionReason);
        Assert.Empty(decision.Economy);
        Assert.Empty(decision.Quotas);
    }

    [Fact]
    public void Decide_QuotaExceededDoesNotDebitWallet()
    {
        var decision = Decide(quotaUsed: 10, quotaLimit: 10);

        Assert.Equal(CubeBetError.DailyRollLimit, decision.Result.Error);
        Assert.Equal("daily_roll_limit", decision.RejectionReason);
        Assert.Empty(decision.Economy);
    }

    [Fact]
    public void Decide_PendingBetReturnsSnapshottedAmount()
    {
        var pending = new DiceCubePendingBet(1, 100, 30, Now, 1, 2, 2);
        var decision = Decide(pending: pending);

        Assert.Equal(CubeBetError.AlreadyPending, decision.Result.Error);
        Assert.Equal(30, decision.Result.PendingAmount);
        Assert.Equal("already_pending", decision.RejectionReason);
    }

    [Fact]
    public void Decide_BusyAndCooldownArePureRejections()
    {
        var busy = Decide(blockingGameId: "darts");
        var cooldown = Decide(cooldownSeconds: 12);

        Assert.Equal(CubeBetError.BusyOtherGame, busy.Result.Error);
        Assert.Equal("darts", busy.Result.BlockingGameId);
        Assert.Equal(CubeBetError.Cooldown, cooldown.Result.Error);
        Assert.Equal(12, cooldown.Result.CooldownSeconds);
        Assert.Empty(busy.Economy);
        Assert.Empty(cooldown.Economy);
    }

    [Fact]
    public void Decide_SameInputProducesEqualDecision()
    {
        var first = Decide();
        var second = Decide();

        Assert.Equal(first.Status, second.Status);
        Assert.Equal(first.NewState, second.NewState);
        Assert.Equal(first.Result, second.Result);
        Assert.Equal(first.Economy, second.Economy);
        Assert.Equal(first.Quotas, second.Quotas);
        Assert.Equal(first.Events, second.Events);
        Assert.Equal(first.RejectionReason, second.RejectionReason);
    }

    private static GameDecision<DiceCubePlaceBetState, CubeBetResult> Decide(
        int amount = 50,
        int maxBet = 100,
        long balance = 100,
        long quotaUsed = 0,
        long quotaLimit = 10,
        DiceCubePendingBet? pending = null,
        int cooldownSeconds = 0,
        string? blockingGameId = null)
    {
        var command = new DiceCubePlaceBetCommand(
            1,
            "player",
            100,
            amount,
            "dicecube:test",
            maxBet,
            1,
            2,
            2,
            cooldownSeconds,
            blockingGameId);
        return new DiceCubePlaceBetAction().Decide(
            new GameActionInput<DiceCubePlaceBetState, DiceCubePlaceBetCommand>(
                command,
                new DiceCubePlaceBetState(pending),
                new WalletSnapshot(balance),
                new Dictionary<string, QuotaSnapshot>(StringComparer.Ordinal)
                {
                    [DiceCubePlaceBetAction.DailyRollQuota] = new(quotaUsed, quotaLimit),
                },
                new EntropyValue([]),
                Now));
    }
}
