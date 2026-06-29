using Games.Pick.Application.Results;
using Games.Pick.Application.Services;
using Games.Pick.Domain.Configuration;
using Games.Pick.Infrastructure.Persistence;
using Games.Pick.Infrastructure.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PickLotteryServiceTests
{
    private static PickLotteryService MakeService(
        FakeLotteryStore store,
        FakeEconomicsService? economics = null,
        PickLotteryOptions? options = null) => new(
            store,
            economics ?? new FakeEconomicsService(),
            new NullAnalyticsService(),
            Options.Create(new PickOptions { Lottery = options ?? new PickLotteryOptions() }),
            NullLogger<PickLotteryService>.Instance);

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task OpenAsync_InvalidStake_IsRejectedWithoutDebit(int stake)
    {
        var economics = new FakeEconomicsService();
        var result = await MakeService(
                new FakeLotteryStore(), economics,
                new PickLotteryOptions { MinStake = 1, MaxStake = 100 })
            .OpenAsync(1, "a", 10, stake, default);

        Assert.Equal(LotteryOpenStatus.InvalidStake, result.Status);
        Assert.Empty(economics.Debits);
    }

    [Fact]
    public async Task OpenAsync_ExistingPool_ReturnsAlreadyOpen()
    {
        var store = new FakeLotteryStore { Open = Row() };

        var result = await MakeService(store).OpenAsync(2, "b", 10, 20, default);

        Assert.Equal(LotteryOpenStatus.AlreadyOpen, result.Status);
        Assert.Equal(store.Open, result.Row);
    }

    [Fact]
    public async Task OpenAsync_InsufficientFunds_ReturnsBalance()
    {
        var economics = new FakeEconomicsService { StartingBalance = 9 };

        var result = await MakeService(new FakeLotteryStore(), economics)
            .OpenAsync(1, "a", 10, 20, default);

        Assert.Equal(LotteryOpenStatus.NotEnoughCoins, result.Status);
        Assert.Equal(9, result.Balance);
    }

    [Fact]
    public async Task OpenAsync_Success_DebitsAndCreatesPoolWithOpenerEntry()
    {
        var store = new FakeLotteryStore();
        var economics = new FakeEconomicsService { StartingBalance = 100 };

        var result = await MakeService(store, economics).OpenAsync(1, "a", 10, 20, default);

        Assert.Equal(LotteryOpenStatus.Ok, result.Status);
        Assert.Equal(80, result.Balance);
        Assert.NotNull(store.Open);
        Assert.Single(store.Entries);
        Assert.Equal(20, store.Entries[0].StakePaid);
    }

    [Fact]
    public async Task OpenAsync_InsertRace_RefundsStake()
    {
        var store = new FakeLotteryStore { RejectOpen = true };
        var economics = new FakeEconomicsService { StartingBalance = 100 };

        var result = await MakeService(store, economics).OpenAsync(1, "a", 10, 20, default);

        Assert.Equal(LotteryOpenStatus.AlreadyOpen, result.Status);
        Assert.Equal(100, result.Balance);
        Assert.Single(economics.Credits);
        Assert.Equal("pick.lottery.open.refund", economics.Credits[0].Reason);
    }

    [Fact]
    public async Task JoinAsync_NoPool_ReturnsNoOpenLottery()
    {
        var result = await MakeService(new FakeLotteryStore()).JoinAsync(2, "b", 10, default);

        Assert.Equal(LotteryJoinStatus.NoOpenLottery, result.Status);
    }

    [Fact]
    public async Task JoinAsync_Success_ReturnsEntrantsPotAndBalance()
    {
        var store = new FakeLotteryStore();
        store.SeedOpen(Row(stake: 20));
        var economics = new FakeEconomicsService { StartingBalance = 100 };

        var result = await MakeService(store, economics).JoinAsync(2, "b", 10, default);

        Assert.Equal(LotteryJoinStatus.Ok, result.Status);
        Assert.Equal(2, result.Entrants);
        Assert.Equal(40, result.PotSoFar);
        Assert.Equal(80, result.Balance);
    }

    [Fact]
    public async Task JoinAsync_Duplicate_RefundsStake()
    {
        var store = new FakeLotteryStore();
        store.SeedOpen(Row(stake: 20));
        store.JoinError = LotteryJoinError.AlreadyJoined;
        var economics = new FakeEconomicsService { StartingBalance = 100 };

        var result = await MakeService(store, economics).JoinAsync(2, "b", 10, default);

        Assert.Equal(LotteryJoinStatus.AlreadyJoined, result.Status);
        Assert.Equal(100, result.Balance);
        Assert.Equal("pick.lottery.join.refund", Assert.Single(economics.Credits).Reason);
    }

    [Fact]
    public async Task InfoAsync_ReturnsEntrantsAndPot()
    {
        var store = new FakeLotteryStore();
        store.SeedOpen(Row(stake: 25));
        store.Entries.Add(Entry(store.Open!.Id, 2, "b", 25));

        var result = await MakeService(store).InfoAsync(10, default);

        Assert.NotNull(result);
        Assert.Equal(2, result.Entrants);
        Assert.Equal(50, result.PotSoFar);
    }

    [Fact]
    public async Task CancelByOpenerAsync_WrongUser_DoesNothing()
    {
        var store = new FakeLotteryStore();
        store.SeedOpen(Row(openerId: 1));

        Assert.Null(await MakeService(store).CancelByOpenerAsync(2, 10, default));
        Assert.False(store.Cancelled);
    }

    [Fact]
    public async Task CancelByOpenerAsync_RefundsEveryEntry()
    {
        var store = new FakeLotteryStore();
        store.SeedOpen(Row(stake: 20));
        store.Entries.Add(Entry(store.Open!.Id, 2, "b", 20));
        var economics = new FakeEconomicsService { StartingBalance = 0 };

        var result = await MakeService(store, economics).CancelByOpenerAsync(1, 10, default);

        Assert.Equal(LotterySettleKind.Cancelled, result?.Kind);
        Assert.True(store.Cancelled);
        Assert.Equal(2, economics.Credits.Count);
        Assert.All(economics.Credits, credit => Assert.Equal("pick.lottery.cancel.refund", credit.Reason));
    }

    [Fact]
    public async Task SettleAsync_BelowQuorum_RefundsAndCancels()
    {
        var store = new FakeLotteryStore();
        store.SeedOpen(Row(stake: 30));
        var economics = new FakeEconomicsService { StartingBalance = 0 };

        var result = await MakeService(store, economics).SettleAsync(store.Open!, default);

        Assert.Equal(LotterySettleKind.Cancelled, result.Kind);
        Assert.True(store.Cancelled);
        Assert.Equal(30, Assert.Single(economics.Credits).Amount);
    }

    [Fact]
    public async Task SettleAsync_WithQuorum_PaysOneWinnerMinusFee()
    {
        var store = new FakeLotteryStore();
        store.SeedOpen(Row(stake: 100));
        store.Entries.Add(Entry(store.Open!.Id, 2, "b", 100));
        var economics = new FakeEconomicsService { StartingBalance = 0 };

        var result = await MakeService(
                store, economics,
                new PickLotteryOptions { MinEntrantsToSettle = 2, HouseFeePercent = 0.05 })
            .SettleAsync(store.Open!, default);

        Assert.Equal(LotterySettleKind.Settled, result.Kind);
        Assert.True(store.Settled);
        Assert.Equal(200, result.Pot);
        Assert.Equal(10, result.Fee);
        Assert.Equal(190, result.Payout);
        Assert.Equal(190, Assert.Single(economics.Credits).Amount);
        Assert.Contains(result.WinnerId, new long?[] { 1, 2 });
    }

    private static PickLotteryRow Row(long openerId = 1, int stake = 20) => new(
        Guid.NewGuid(), 10, openerId, "a", stake, "open", DateTime.UtcNow,
        DateTime.UtcNow.AddMinutes(5), null, null, null, null, null, null);

    private static PickLotteryEntryRow Entry(Guid id, long userId, string name, int stake) =>
        new(id, userId, name, stake, DateTime.UtcNow);

    private sealed class FakeLotteryStore : IPickLotteryStore
    {
        public PickLotteryRow? Open { get; set; }
        public List<PickLotteryEntryRow> Entries { get; } = [];
        public bool RejectOpen { get; set; }
        public LotteryJoinError JoinError { get; set; }
        public bool Cancelled { get; private set; }
        public bool Settled { get; private set; }

        public void SeedOpen(PickLotteryRow row)
        {
            Open = row;
            Entries.Add(Entry(row.Id, row.OpenerId, row.OpenerName, row.Stake));
        }

        public Task<(LotteryOpenError Err, PickLotteryRow? Row)> InsertOpenAsync(
            Guid id, long chatId, long openerId, string openerName, int stake, DateTime deadlineUtc, CancellationToken ct)
        {
            if (RejectOpen)
            {
                Open = Row(openerId: 99, stake: stake);
                return Task.FromResult<(LotteryOpenError, PickLotteryRow?)>((LotteryOpenError.AlreadyOpenInChat, null));
            }
            Open = new PickLotteryRow(id, chatId, openerId, openerName, stake, "open", DateTime.UtcNow,
                deadlineUtc, null, null, null, null, null, null);
            Entries.Add(Entry(id, openerId, openerName, stake));
            return Task.FromResult<(LotteryOpenError, PickLotteryRow?)>((LotteryOpenError.None, Open));
        }

        public Task<PickLotteryRow?> FindOpenByChatAsync(long chatId, CancellationToken ct) =>
            Task.FromResult(Open is { Status: "open" } && Open.ChatId == chatId ? Open : null);

        public Task<PickLotteryRow?> FindByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Open?.Id == id ? Open : null);

        public Task<(LotteryJoinError Err, PickLotteryRow? Row)> AddEntryAsync(
            long chatId, long userId, string displayName, CancellationToken ct)
        {
            if (JoinError != LotteryJoinError.None)
                return Task.FromResult<(LotteryJoinError, PickLotteryRow?)>((JoinError, Open));
            if (Open is null)
                return Task.FromResult<(LotteryJoinError, PickLotteryRow?)>((LotteryJoinError.NoOpenLottery, null));
            Entries.Add(Entry(Open.Id, userId, displayName, Open.Stake));
            return Task.FromResult<(LotteryJoinError, PickLotteryRow?)>((LotteryJoinError.None, Open));
        }

        public Task<IReadOnlyList<PickLotteryEntryRow>> ListEntriesAsync(Guid lotteryId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PickLotteryEntryRow>>([.. Entries.Where(entry => entry.LotteryId == lotteryId)]);

        public Task<IReadOnlyList<PickLotteryRow>> ListExpiredOpenAsync(int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PickLotteryRow>>(Open is null ? [] : [Open]);

        public Task<bool> MarkSettledAsync(
            Guid id, long winnerId, string winnerName, int potTotal, int payout, int fee, CancellationToken ct)
        {
            Settled = true;
            Open = Open! with { Status = "settled", WinnerId = winnerId, WinnerName = winnerName };
            return Task.FromResult(true);
        }

        public Task<bool> MarkCancelledAsync(Guid id, CancellationToken ct)
        {
            Cancelled = true;
            Open = Open! with { Status = "cancelled" };
            return Task.FromResult(true);
        }
    }
}
