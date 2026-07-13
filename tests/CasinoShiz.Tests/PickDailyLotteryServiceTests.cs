using BotFramework.Sdk.Execution;
using Games.Pick.Application.Execution;
using Games.Pick.Application.Results;
using Games.Pick.Domain.Events;
using Games.Pick.Infrastructure.Models;
using Games.Pick.Infrastructure.Persistence;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PickDailyLotteryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 13, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, DailyBuyStatus.InvalidCount)]
    [InlineData(-1, DailyBuyStatus.InvalidCount)]
    [InlineData(4, DailyBuyStatus.OverPerCommandCap)]
    public void Buy_InvalidCount_HasNoEffects(int count, DailyBuyStatus expected)
    {
        var command = BuyCommand(count, perCommandCap: 3);
        var decision = new DailyBuyAction().Decide(Input(command, new(Row(), []), 500));

        Assert.Equal(DecisionStatus.Rejected, decision.Status);
        Assert.Equal(expected, decision.Result.Status);
        Assert.Empty(decision.Economy);
    }

    [Fact]
    public void Buy_DailyCapIncludesExistingTickets()
    {
        var row = Row();
        var state = new DailyLotteryState(row, [new(1, "a"), new(1, "a")]);
        var decision = new DailyBuyAction().Decide(Input(BuyCommand(2, userCap: 3), state, 500));

        Assert.Equal(DailyBuyStatus.OverDailyCap, decision.Result.Status);
        Assert.Equal(2, decision.Result.TotalUserTickets);
        Assert.Empty(decision.Economy);
    }

    [Fact]
    public void Buy_Success_DebitsAllTicketsAsOneDecision()
    {
        var row = Row();
        var decision = new DailyBuyAction().Decide(Input(BuyCommand(3), new(row, []), 500));

        Assert.Equal(DecisionStatus.Accepted, decision.Status);
        Assert.Equal(350, decision.Result.Balance);
        Assert.Equal(3, decision.NewState.Tickets.Count);
        Assert.Equal(EconomyEffectKind.Debit, Assert.Single(decision.Economy).Kind);
        Assert.Equal(150, Assert.Single(decision.Economy).Amount);
        Assert.IsType<PickDailyTicketsBought>(Assert.Single(decision.Events));
    }

    [Fact]
    public void Settlement_EmptyPool_CancelsWithoutWalletEffect()
    {
        var row = Row();
        var command = new DailySettleCommand(row, [], "daily-empty", 0.10);
        var decision = new DailySettleAction().Decide(Input(command, new(row, []), 0));

        Assert.False(decision.Result.Drawn);
        Assert.Null(decision.CustomEffects);
        Assert.True(Assert.IsType<PickDailyLotteryCompleted>(Assert.Single(decision.Events)).Cancelled);
    }

    [Fact]
    public void Settlement_WeightedEntropyPaysWinnerMinusFee()
    {
        var row = Row();
        DailyTicketOwner[] tickets = [new(1, "a"), new(2, "b"), new(2, "b"), new(2, "b")];
        var command = new DailySettleCommand(row, tickets, "daily-win", 0.10);
        var entropy = new Dictionary<string, double> { [DailySettleAction.WinnerEntropy] = 0.75 };
        var decision = new DailySettleAction().Decide(Input(command, new(row, tickets), 0, entropy));

        Assert.True(decision.Result.Drawn);
        Assert.Equal(2, decision.Result.WinnerId);
        Assert.Equal(180, decision.Result.Payout);
        Assert.Equal(3, decision.Result.WinnerTicketCount);
        Assert.Equal(180, Assert.IsType<PickWalletCreditEffect>(Assert.Single(decision.CustomEffects!)).Amount);
    }

    private static DailyBuyCommand BuyCommand(int count, int perCommandCap = 3, int userCap = 10) =>
        new(1, "a", 10, count, $"daily-buy-{count}", new DateOnly(2026, 7, 14),
            Now.AddHours(12).UtcDateTime, 50, perCommandCap, userCap);

    private static GameActionInput<DailyLotteryState, TCommand> Input<TCommand>(
        TCommand command, DailyLotteryState state, long balance,
        IReadOnlyDictionary<string, double>? entropy = null) =>
        new(command, state, new WalletSnapshot(balance),
            new Dictionary<string, QuotaSnapshot>(), new EntropyValue(entropy ?? new Dictionary<string, double>()), Now);

    private static PickDailyLotteryRow Row() => new(
        Guid.NewGuid(), 10, new DateOnly(2026, 7, 14), 50, "open", Now.UtcDateTime,
        Now.AddHours(12).UtcDateTime, null, null, null, null, null, null, null);
}
