using BotFramework.Sdk.Execution;
using Games.Pick.Application.Execution;
using Games.Pick.Application.Results;
using Games.Pick.Domain.Events;
using Games.Pick.Infrastructure.Results;
using Games.Pick.Infrastructure.Persistence;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PickLotteryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Open_InvalidStake_HasNoEffects(int stake)
    {
        var command = new QuickLotteryOpenCommand(1, "a", 10, stake, $"open-{stake}", 1, 100, 300);
        var decision = new QuickLotteryOpenAction().Decide(Input(command, new(null, []), 100));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(LotteryOpenStatus.InvalidStake, decision.Result.Status);
        Assert.Empty(decision.Economy);
    }

    [Fact]
    public void Open_Success_DebitsAndCreatesPoolAtomically()
    {
        var command = new QuickLotteryOpenCommand(1, "a", 10, 20, "open-ok", 1, 100, 300);
        var decision = new QuickLotteryOpenAction().Decide(Input(command, new(null, []), 100));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(80, decision.Result.Balance);
        Assert.Single(decision.NewState.Entries);
        Assert.Equal(EconomyEffectKind.Debit, Assert.Single(decision.Economy).Kind);
        Assert.Equal(20, Assert.Single(decision.Economy).Amount);
        Assert.IsType<PickLotteryOpened>(Assert.Single(decision.Events));
    }

    [Fact]
    public void Join_Duplicate_HasNoDebitOrRefund()
    {
        var row = Row();
        var entries = new[] { Entry(row, 1, "a") };
        var command = new QuickLotteryJoinCommand(1, "a", row.ChatId, "join-duplicate");
        var decision = new QuickLotteryJoinAction().Decide(Input(command, new(row, entries), 100));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(LotteryJoinStatus.AlreadyJoined, decision.Result.Status);
        Assert.Empty(decision.Economy);
        Assert.Null(decision.CustomEffects);
    }

    [Fact]
    public void Settlement_BelowQuorum_ProducesRefundEffects()
    {
        var row = Row(stake: 30);
        var entries = new[] { Entry(row, 1, "a") };
        var command = new QuickLotterySettleCommand(row, entries, false, "settle-cancel", 2, 0.05);
        var decision = new QuickLotterySettleAction().Decide(Input(command, new(row, entries), 0));

        Assert.Equal(LotterySettleKind.Cancelled, decision.Result.Kind);
        var refund = Assert.IsType<PickWalletCreditEffect>(Assert.Single(decision.CustomEffects!));
        Assert.Equal(30, refund.Amount);
        Assert.True(Assert.IsType<PickLotteryCompleted>(Assert.Single(decision.Events)).Cancelled);
    }

    [Fact]
    public void Settlement_UsesFrameworkEntropyAndPaysOneWinner()
    {
        var row = Row(stake: 100);
        var entries = new[] { Entry(row, 1, "a"), Entry(row, 2, "b") };
        var command = new QuickLotterySettleCommand(row, entries, false, "settle-win", 2, 0.05);
        var decision = new QuickLotterySettleAction().Decide(Input(
            command, new(row, entries), 0,
            new Dictionary<string, double> { [QuickLotterySettleAction.WinnerEntropy] = 0.75 }));

        Assert.Equal(LotterySettleKind.Settled, decision.Result.Kind);
        Assert.Equal(2, decision.Result.WinnerId);
        Assert.Equal(190, decision.Result.Payout);
        Assert.Equal(190, Assert.IsType<PickWalletCreditEffect>(Assert.Single(decision.CustomEffects!)).Amount);
    }

    private static GameActionInput<QuickLotteryState, TCommand> Input<TCommand>(
        TCommand command, QuickLotteryState state, long balance,
        IReadOnlyDictionary<string, double>? entropy = null) =>
        new(command, state, new WalletSnapshot(balance),
            new Dictionary<string, QuotaSnapshot>(), new EntropyValue(entropy ?? new Dictionary<string, double>()), Now);

    private static PickLotteryRow Row(int stake = 20) => new(
        Guid.NewGuid(), 10, 1, "a", stake, "open", Now.UtcDateTime,
        Now.AddMinutes(5).UtcDateTime, null, null, null, null, null, null);

    private static PickLotteryEntryRow Entry(PickLotteryRow row, long userId, string name) =>
        new(row.Id, userId, name, row.Stake, Now.UtcDateTime);
}
