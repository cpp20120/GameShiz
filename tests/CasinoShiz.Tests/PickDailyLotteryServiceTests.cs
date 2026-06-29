using Games.Pick.Application.Services;
using Games.Pick.Application.Results;
using Games.Pick.Domain.Configuration;
using Games.Pick.Infrastructure.Models;
using Games.Pick.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PickDailyLotteryServiceTests
{
    private static PickDailyLotteryService MakeService(
        FakeDailyStore store,
        FakeEconomicsService? economics = null,
        PickDailyLotteryOptions? daily = null) => new(
            store,
            economics ?? new FakeEconomicsService(),
            new NullAnalyticsService(),
            Options.Create(new PickOptions { Daily = daily ?? new PickDailyLotteryOptions() }),
            Options.Create(new TelegramDiceDailyLimitOptions { TimezoneOffsetHours = 0 }),
            NullLogger<PickDailyLotteryService>.Instance);

    [Theory]
    [InlineData(0, DailyBuyStatus.InvalidCount)]
    [InlineData(-1, DailyBuyStatus.InvalidCount)]
    [InlineData(4, DailyBuyStatus.OverPerCommandCap)]
    public async Task BuyAsync_InvalidCount_IsRejected(int count, DailyBuyStatus expected)
    {
        var result = await MakeService(
                new FakeDailyStore(),
                daily: new PickDailyLotteryOptions { MaxTicketsPerBuyCommand = 3 })
            .BuyAsync(1, "a", 10, count, default);

        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task BuyAsync_ClosingRow_IsRejected()
    {
        var store = new FakeDailyStore { ForceClosing = true };

        var result = await MakeService(store).BuyAsync(1, "a", 10, 1, default);

        Assert.Equal(DailyBuyStatus.DayAlreadyClosing, result.Status);
    }

    [Fact]
    public async Task BuyAsync_DailyCapIncludesOwnedTickets()
    {
        var store = new FakeDailyStore { OwnedTickets = 2 };

        var result = await MakeService(
                store,
                daily: new PickDailyLotteryOptions { MaxTicketsPerUserPerDay = 3 })
            .BuyAsync(1, "a", 10, 2, default);

        Assert.Equal(DailyBuyStatus.OverDailyCap, result.Status);
        Assert.Equal(2, result.TotalUserTickets);
    }

    [Fact]
    public async Task BuyAsync_InsufficientFunds_ReturnsBalance()
    {
        var economics = new FakeEconomicsService { StartingBalance = 49 };

        var result = await MakeService(new FakeDailyStore(), economics)
            .BuyAsync(1, "a", 10, 1, default);

        Assert.Equal(DailyBuyStatus.NotEnoughCoins, result.Status);
        Assert.Equal(49, result.Balance);
    }

    [Fact]
    public async Task BuyAsync_Success_DebitsAndReturnsTicketTotals()
    {
        var store = new FakeDailyStore();
        var economics = new FakeEconomicsService { StartingBalance = 500 };

        var result = await MakeService(store, economics).BuyAsync(1, "a", 10, 3, default);

        Assert.Equal(DailyBuyStatus.Ok, result.Status);
        Assert.Equal(3, result.TicketsBought);
        Assert.Equal(3, result.TotalUserTickets);
        Assert.Equal(3, result.TotalTickets);
        Assert.Equal(150, result.PotTotal);
        Assert.Equal(350, result.Balance);
        Assert.Equal(150, Assert.Single(economics.Debits).Amount);
    }

    [Fact]
    public async Task BuyAsync_PartialInsert_RefundsMissingTickets()
    {
        var store = new FakeDailyStore { InsertedOverride = 1 };
        var economics = new FakeEconomicsService { StartingBalance = 500 };

        await MakeService(store, economics).BuyAsync(1, "a", 10, 3, default);

        var refund = Assert.Single(economics.Credits);
        Assert.Equal(100, refund.Amount);
        Assert.Equal("pick.daily.buy.refund", refund.Reason);
    }

    [Fact]
    public async Task InfoAsync_ReturnsViewerAndTopHolderCounts()
    {
        var store = new FakeDailyStore();
        store.Summaries.AddRange([
            new PickDailyTicketSummary(1, "a", 3),
            new PickDailyTicketSummary(2, "b", 2),
        ]);

        var result = await MakeService(store).InfoAsync(10, 2, default);

        Assert.NotNull(result);
        Assert.Equal(5, result.TicketsTotal);
        Assert.Equal(2, result.DistinctUsers);
        Assert.Equal(250, result.PotTotal);
        Assert.Equal(2, result.ViewerTickets);
    }

    [Fact]
    public async Task SettleAsync_EmptyPool_CancelsWithoutPayout()
    {
        var store = new FakeDailyStore();

        var result = await MakeService(store).SettleAsync(store.Row, default);

        Assert.False(result.Drawn);
        Assert.True(store.Cancelled);
        Assert.Equal(0, result.Payout);
    }

    [Fact]
    public async Task SettleAsync_WithTickets_PaysWinnerMinusFee()
    {
        var store = new FakeDailyStore
        {
            TicketCount = 4,
            DistinctUsers = 2,
            Winner = (2, "b"),
            OwnedTickets = 3,
        };
        var economics = new FakeEconomicsService { StartingBalance = 0 };

        var result = await MakeService(
                store, economics,
                new PickDailyLotteryOptions { HouseFeePercent = 0.10 })
            .SettleAsync(store.Row, default);

        Assert.True(result.Drawn);
        Assert.True(store.Settled);
        Assert.Equal(200, result.PotTotal);
        Assert.Equal(20, result.Fee);
        Assert.Equal(180, result.Payout);
        Assert.Equal(2, result.WinnerId);
        Assert.Equal(3, result.WinnerTicketCount);
        Assert.Equal(180, Assert.Single(economics.Credits).Amount);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(99, 30)]
    [InlineData(7, 7)]
    public async Task HistoryAsync_ClampsLimit(int requested, int expected)
    {
        var store = new FakeDailyStore();

        await MakeService(store).HistoryAsync(10, requested, default);

        Assert.Equal(expected, store.LastHistoryLimit);
    }

    private sealed class FakeDailyStore : IPickDailyLotteryStore
    {
        public PickDailyLotteryRow Row { get; private set; } = NewRow();
        public bool ForceClosing { get; set; }
        public int OwnedTickets { get; set; }
        public int TicketCount { get; set; }
        public int DistinctUsers { get; set; }
        public int? InsertedOverride { get; set; }
        public (long UserId, string DisplayName)? Winner { get; set; }
        public List<PickDailyTicketSummary> Summaries { get; } = [];
        public bool Cancelled { get; private set; }
        public bool Settled { get; private set; }
        public int LastHistoryLimit { get; private set; }

        public Task<PickDailyLotteryRow> GetOrCreateOpenAsync(
            long chatId, DateOnly dayLocal, int ticketPrice, DateTime deadlineUtc, CancellationToken ct)
        {
            Row = new PickDailyLotteryRow(
                Row.Id, chatId, dayLocal, ticketPrice, "open", DateTime.UtcNow,
                ForceClosing ? DateTime.UtcNow.AddSeconds(-1) : deadlineUtc,
                null, null, null, null, null, null, null);
            return Task.FromResult(Row);
        }

        public Task<PickDailyLotteryRow?> FindOpenByChatAsync(long chatId, DateOnly dayLocal, CancellationToken ct) =>
            Task.FromResult<PickDailyLotteryRow?>(Row.ChatId == chatId ? Row : null);

        public Task<int> InsertTicketsAsync(
            Guid lotteryId, long userId, string displayName, int count, int pricePaid, CancellationToken ct)
        {
            var inserted = InsertedOverride ?? count;
            TicketCount += inserted;
            OwnedTickets += inserted;
            return Task.FromResult(inserted);
        }

        public Task<int> CountTicketsAsync(Guid lotteryId, CancellationToken ct) => Task.FromResult(TicketCount);
        public Task<int> CountUserTicketsAsync(Guid lotteryId, long userId, CancellationToken ct) => Task.FromResult(OwnedTickets);
        public Task<int> CountDistinctUsersAsync(Guid lotteryId, CancellationToken ct) => Task.FromResult(DistinctUsers);
        public Task<IReadOnlyList<PickDailyTicketSummary>> ListUserTicketCountsAsync(Guid lotteryId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PickDailyTicketSummary>>(Summaries);
        public Task<IReadOnlyList<PickDailyLotteryRow>> ListExpiredOpenAsync(int limit, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<PickDailyLotteryRow>>([Row]);
        public Task<(long UserId, string DisplayName)?> PickRandomWinnerAsync(Guid lotteryId, CancellationToken ct) =>
            Task.FromResult(Winner);
        public Task<bool> MarkSettledAsync(
            Guid id, long winnerId, string winnerName, int ticketCount, int potTotal, int payout, int fee, CancellationToken ct)
        {
            Settled = true;
            return Task.FromResult(true);
        }
        public Task<bool> MarkCancelledAsync(Guid id, CancellationToken ct)
        {
            Cancelled = true;
            return Task.FromResult(true);
        }
        public Task<IReadOnlyList<PickDailyLotteryRow>> ListHistoryAsync(long chatId, int limit, CancellationToken ct)
        {
            LastHistoryLimit = limit;
            return Task.FromResult<IReadOnlyList<PickDailyLotteryRow>>([Row]);
        }

        private static PickDailyLotteryRow NewRow() => new(
            Guid.NewGuid(), 10, DateOnly.FromDateTime(DateTime.UtcNow), 50, "open",
            DateTime.UtcNow, DateTime.UtcNow.AddHours(1), null, null, null, null, null, null, null);
    }
}
