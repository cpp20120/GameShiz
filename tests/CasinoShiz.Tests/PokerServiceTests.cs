using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasinoShiz.Tests;

public sealed class PokerServiceTests
{
    private static PokerService MakeService(
        MemoryPokerTables? tables = null,
        MemoryPokerSeats? seats = null,
        FakeEconomicsService? economics = null,
        RecordingAnalyticsService? analytics = null,
        NullEventBus? events = null,
        PokerOptions? options = null) =>
        new(tables ?? new MemoryPokerTables(), seats ?? new MemoryPokerSeats(),
            economics ?? new FakeEconomicsService(), analytics ?? new RecordingAnalyticsService(),
            events ?? new NullEventBus(), new FakeRuntimeTuning { Poker = options ?? new PokerOptions() },
            NullLogger<PokerService>.Instance);

    [Fact]
    public async Task FindMyTableAsync_NoOpenTable_ReturnsEmpty()
    {
        var result = await MakeService().FindMyTableAsync(1, 100, default);
        Assert.Null(result.Snapshot);
        Assert.Null(result.MySeat);
    }

    [Fact]
    public async Task FindMyTableAsync_UserNotSeated_ReturnsEmpty()
    {
        var tables = new MemoryPokerTables();
        tables.Items["ABC123"] = Table("ABC123", 100, 1);
        var result = await MakeService(tables).FindMyTableAsync(2, 100, default);
        Assert.Null(result.Snapshot);
        Assert.Null(result.MySeat);
    }

    [Fact]
    public async Task FindMyTableAsync_SeatedUser_ReturnsSnapshotAndSeat()
    {
        var tables = new MemoryPokerTables();
        var seats = new MemoryPokerSeats();
        tables.Items["ABC123"] = Table("ABC123", 100, 1);
        seats.Items.Add(Seat("ABC123", 0, 1, "alice", 100));

        var result = await MakeService(tables, seats).FindMyTableAsync(1, 100, default);

        Assert.NotNull(result.Snapshot);
        Assert.Equal(1, result.MySeat?.UserId);
    }

    [Fact]
    public async Task CreateTableAsync_ExistingChatTable_IsRejected()
    {
        var tables = new MemoryPokerTables();
        tables.Items["ABC123"] = Table("ABC123", 100, 1);
        var result = await MakeService(tables).CreateTableAsync(2, "bob", 100, default);
        Assert.Equal(PokerError.TableAlreadyExists, result.Error);
    }

    [Fact]
    public async Task CreateTableAsync_InsufficientBalance_IsRejected()
    {
        var economics = new FakeEconomicsService { StartingBalance = 99 };
        var result = await MakeService(economics: economics).CreateTableAsync(1, "alice", 100, default);
        Assert.Equal(PokerError.NotEnoughCoins, result.Error);
    }

    [Fact]
    public async Task CreateTableAsync_SuccessPersistsDebitsPublishesAndTracks()
    {
        var tables = new MemoryPokerTables();
        var seats = new MemoryPokerSeats();
        var economics = new FakeEconomicsService();
        var analytics = new RecordingAnalyticsService();
        var events = new NullEventBus();

        var result = await MakeService(tables, seats, economics, analytics, events)
            .CreateTableAsync(1, "alice", 100, sourceMessageId: 42, default);

        Assert.Equal(PokerError.None, result.Error);
        Assert.Equal(100, result.BuyIn);
        Assert.Single(tables.Items);
        Assert.Single(seats.Items);
        Assert.Single(economics.Debits);
        Assert.Single(events.Published.OfType<PokerTableCreated>());
        Assert.Contains(analytics.Events, x => x.EventName == "create");
    }

    [Fact]
    public async Task JoinTableAsync_MissingAndWrongChatTables_AreRejected()
    {
        var tables = new MemoryPokerTables();
        tables.Items["ABC123"] = Table("ABC123", 100, 1);
        var service = MakeService(tables);

        Assert.Equal(PokerError.TableNotFound,
            (await service.JoinTableAsync(2, "bob", 100, "missing", default)).Error);
        Assert.Equal(PokerError.TableNotFound,
            (await service.JoinTableAsync(2, "bob", 200, "abc123", default)).Error);
    }

    [Fact]
    public async Task JoinTableAsync_AlreadySeated_IsRejected()
    {
        var tables = new MemoryPokerTables();
        var seats = new MemoryPokerSeats();
        tables.Items["ABC123"] = Table("ABC123", 100, 1);
        seats.Items.Add(Seat("ABC123", 0, 2, "bob", 100));

        var result = await MakeService(tables, seats).JoinTableAsync(2, "bob", 100, "abc123", default);
        Assert.Equal(PokerError.AlreadySeated, result.Error);
    }

    [Fact]
    public async Task JoinTableAsync_InsufficientBalance_IsRejected()
    {
        var tables = new MemoryPokerTables();
        tables.Items["ABC123"] = Table("ABC123", 100, 1);
        var result = await MakeService(tables, economics: new FakeEconomicsService { StartingBalance = 10 })
            .JoinTableAsync(2, "bob", 100, "abc123", default);
        Assert.Equal(PokerError.NotEnoughCoins, result.Error);
    }

    [Fact]
    public async Task JoinTableAsync_FullTable_IsRejected()
    {
        var tables = new MemoryPokerTables();
        var seats = new MemoryPokerSeats();
        tables.Items["ABC123"] = Table("ABC123", 100, 1);
        seats.Items.Add(Seat("ABC123", 0, 1, "alice", 100));
        seats.Items.Add(Seat("ABC123", 1, 2, "bob", 100));

        var result = await MakeService(tables, seats, options: new PokerOptions { MaxPlayers = 2 })
            .JoinTableAsync(3, "carol", 100, "abc123", default);
        Assert.Equal(PokerError.TableFull, result.Error);
    }

    [Fact]
    public async Task JoinTableAsync_SuccessUsesFreeSeatAndEmitsEvent()
    {
        var tables = new MemoryPokerTables();
        var seats = new MemoryPokerSeats();
        var analytics = new RecordingAnalyticsService();
        var events = new NullEventBus();
        tables.Items["ABC123"] = Table("ABC123", 100, 1);
        seats.Items.Add(Seat("ABC123", 0, 1, "alice", 100));
        seats.Items.Add(Seat("ABC123", 2, 3, "carol", 100));

        var result = await MakeService(tables, seats, analytics: analytics, events: events)
            .JoinTableAsync(2, "bob", 100, "abc123", sourceMessageId: 77, default);

        Assert.Equal(PokerError.None, result.Error);
        Assert.Equal(3, result.Seated);
        Assert.Contains(seats.Items, x => x.UserId == 2 && x.Position == 1);
        Assert.Single(events.Published.OfType<PokerPlayerJoined>());
        Assert.Contains(analytics.Events, x => x.EventName == "join");
    }

    [Fact]
    public async Task StartHandAsync_NoTableOrSeat_ReturnsNoTable()
    {
        Assert.Equal(PokerError.NoTable, (await MakeService().StartHandAsync(1, 100, default)).Error);

        var tables = new MemoryPokerTables();
        tables.Items["ABC123"] = Table("ABC123", 100, 1);
        Assert.Equal(PokerError.NoTable,
            (await MakeService(tables).StartHandAsync(1, 100, default)).Error);
    }

    [Fact]
    public async Task StartHandAsync_NonHostAndActiveHand_AreRejected()
    {
        var tables = new MemoryPokerTables();
        var seats = new MemoryPokerSeats();
        var table = Table("ABC123", 100, 1);
        tables.Items[table.InviteCode] = table;
        seats.Items.Add(Seat(table.InviteCode, 0, 1, "alice", 100));
        seats.Items.Add(Seat(table.InviteCode, 1, 2, "bob", 100));

        Assert.Equal(PokerError.NotHost,
            (await MakeService(tables, seats).StartHandAsync(2, 100, default)).Error);
        table.Status = PokerTableStatus.HandActive;
        Assert.Equal(PokerError.HandInProgress,
            (await MakeService(tables, seats).StartHandAsync(1, 100, default)).Error);
    }

    [Fact]
    public async Task StartHandAsync_RequiresTwoFundedPlayers()
    {
        var tables = new MemoryPokerTables();
        var seats = new MemoryPokerSeats();
        tables.Items["ABC123"] = Table("ABC123", 100, 1);
        seats.Items.Add(Seat("ABC123", 0, 1, "alice", 100));
        var empty = Seat("ABC123", 1, 2, "bob", 100);
        empty.Stack = 0;
        seats.Items.Add(empty);

        var result = await MakeService(tables, seats).StartHandAsync(1, 100, default);
        Assert.Equal(PokerError.NeedTwo, result.Error);
    }

    [Fact]
    public async Task StartHandAsync_SuccessUpdatesStatePublishesAndTracks()
    {
        var tables = new MemoryPokerTables();
        var seats = new MemoryPokerSeats();
        var analytics = new RecordingAnalyticsService();
        var events = new NullEventBus();
        var table = Table("ABC123", 100, 1);
        tables.Items[table.InviteCode] = table;
        seats.Items.Add(Seat(table.InviteCode, 0, 1, "alice", 100));
        seats.Items.Add(Seat(table.InviteCode, 1, 2, "bob", 100));

        var result = await MakeService(tables, seats, analytics: analytics, events: events)
            .StartHandAsync(1, 100, default);

        Assert.Equal(PokerError.None, result.Error);
        Assert.Equal(PokerTableStatus.HandActive, table.Status);
        Assert.NotEmpty(table.DeckState);
        Assert.Single(events.Published.OfType<PokerHandStarted>());
        Assert.Contains(analytics.Events, x => x.EventName == "hand_start");
    }

    private static PokerTable Table(string code, long chatId, long hostId) => new()
    {
        InviteCode = code, ChatId = chatId, HostUserId = hostId,
        Status = PokerTableStatus.Seating, SmallBlind = 1, BigBlind = 2,
    };

    private static PokerSeat Seat(string code, int position, long userId, string name, long chatId) => new()
    {
        InviteCode = code, Position = position, UserId = userId, DisplayName = name,
        ChatId = chatId, Stack = 100,
    };

    private sealed class MemoryPokerTables : IPokerTableStore
    {
        public Dictionary<string, PokerTable> Items { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Task<PokerTable?> FindAsync(string inviteCode, CancellationToken ct) =>
            Task.FromResult(Items.GetValueOrDefault(inviteCode));
        public Task<PokerTable?> FindOpenByChatAsync(long chatId, CancellationToken ct) =>
            Task.FromResult(Items.Values.FirstOrDefault(x => x.ChatId == chatId && x.Status != PokerTableStatus.Closed));
        public Task<bool> CodeExistsAsync(string inviteCode, CancellationToken ct) =>
            Task.FromResult(Items.ContainsKey(inviteCode));
        public Task InsertAsync(PokerTable table, CancellationToken ct) { Items.Add(table.InviteCode, table); return Task.CompletedTask; }
        public Task UpdateAsync(PokerTable table, CancellationToken ct) { Items[table.InviteCode] = table; return Task.CompletedTask; }
        public Task UpsertStateMessageAsync(string inviteCode, int messageId, CancellationToken ct) { Items[inviteCode].StateMessageId = messageId; return Task.CompletedTask; }
        public Task<IReadOnlyList<string>> ListStuckCodesAsync(long cutoffMs, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>(Items.Values.Where(x => x.LastActionAt < cutoffMs).Select(x => x.InviteCode).ToList());
    }

    private sealed class MemoryPokerSeats : IPokerSeatStore
    {
        public List<PokerSeat> Items { get; } = [];
        public Task<PokerSeat?> FindByUserAsync(long userId, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(x => x.UserId == userId));
        public Task<PokerSeat?> FindByUserInTableAsync(long userId, string inviteCode, CancellationToken ct) =>
            Task.FromResult(Items.FirstOrDefault(x => x.UserId == userId && string.Equals(x.InviteCode, inviteCode, StringComparison.OrdinalIgnoreCase)));
        public Task<List<PokerSeat>> ListByTableAsync(string inviteCode, CancellationToken ct) =>
            Task.FromResult(Items.Where(x => string.Equals(x.InviteCode, inviteCode, StringComparison.OrdinalIgnoreCase)).ToList());
        public Task<int> CountByTableAsync(string inviteCode, long exceptUserId, CancellationToken ct) =>
            Task.FromResult(Items.Count(x => x.InviteCode == inviteCode && x.UserId != exceptUserId));
        public Task<bool> AnyForUserAsync(long userId, CancellationToken ct) => Task.FromResult(Items.Any(x => x.UserId == userId));
        public Task InsertAsync(PokerSeat seat, CancellationToken ct) { Items.Add(seat); return Task.CompletedTask; }
        public Task UpdateAsync(PokerSeat seat, CancellationToken ct) => Task.CompletedTask;
        public Task DeleteAsync(string inviteCode, int position, CancellationToken ct) { Items.RemoveAll(x => x.InviteCode == inviteCode && x.Position == position); return Task.CompletedTask; }
        public Task UpsertStateMessageAsync(long userId, int messageId, CancellationToken ct) { var seat = Items.First(x => x.UserId == userId); seat.StateMessageId = messageId; return Task.CompletedTask; }
    }
}
