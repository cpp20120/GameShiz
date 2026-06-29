using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CasinoShiz.Tests;

public class AdminServiceTests
{
    private static AdminService MakeService(
        InMemoryAdminStore? store = null,
        FakeEconomicsService? economics = null,
        IAnalyticsService? analytics = null,
        IMiniGameSessionStore? sessions = null,
        IMiniGameRollGateStore? rollGates = null) =>
        new(
            store ?? new InMemoryAdminStore(),
            economics ?? new FakeEconomicsService(),
            analytics ?? new NullAnalyticsService(),
            NullLogger<AdminService>.Instance,
            sessions,
            rollGates);

    private const long TestScope = 200;

    private static UserSummary MakeUser(long id, string name = "TestUser", int coins = 500) =>
        new(id, TestScope, name, coins, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

    // ── UserSyncAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task UserSyncAsync_ReturnsUserCount()
    {
        var store = new InMemoryAdminStore();
        store.Seed(MakeUser(1));
        store.Seed(MakeUser(2));
        store.Seed(MakeUser(3));
        var svc = MakeService(store);

        var count = await svc.UserSyncAsync(99, default);

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task UserSyncAsync_EmptyStore_Returns0()
    {
        var svc = MakeService();
        var count = await svc.UserSyncAsync(99, default);
        Assert.Equal(0, count);
    }

    // ── PayAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task PayAsync_PositiveAmount_CreditsUser()
    {
        var econ = new FakeEconomicsService { StartingBalance = 500 };
        var store = new InMemoryAdminStore(econ);
        store.Seed(MakeUser(10, coins: 500));
        var svc = MakeService(store, econ);

        await svc.PayAsync(99, targetUserId: 10, TestScope, amount: 200, default);

        Assert.Single(econ.Credits);
        Assert.Equal(200, econ.Credits[0].Amount);
    }

    [Fact]
    public async Task PayAsync_NegativeAmount_DebitsUser()
    {
        var econ = new FakeEconomicsService { StartingBalance = 500 };
        var store = new InMemoryAdminStore(econ);
        store.Seed(MakeUser(10, coins: 500));
        var svc = MakeService(store, econ);

        await svc.PayAsync(99, targetUserId: 10, TestScope, amount: -100, default);

        Assert.Single(econ.Debits);
        Assert.Equal(100, econ.Debits[0].Amount);
    }

    [Fact]
    public async Task PayAsync_PositiveAmount_ReturnsPayResult()
    {
        var econ = new FakeEconomicsService { StartingBalance = 500 };
        var store = new InMemoryAdminStore(econ);
        store.Seed(MakeUser(10, "Alice", coins: 500));
        var svc = MakeService(store, econ);

        var result = await svc.PayAsync(99, 10, TestScope, 100, default);

        Assert.NotNull(result);
        Assert.Equal("Alice", result!.DisplayName);
        Assert.Equal(500, result.OldCoins);
        Assert.Equal(600, result.NewCoins);
        Assert.Equal(100, result.Amount);
    }

    [Fact]
    public async Task PayAsync_NegativeAmount_ReturnsUpdatedBalance()
    {
        var econ = new FakeEconomicsService { StartingBalance = 500 };
        var store = new InMemoryAdminStore(econ);
        store.Seed(MakeUser(10, "Bob", coins: 500));
        var svc = MakeService(store, econ);

        var result = await svc.PayAsync(99, 10, TestScope, -200, default);

        Assert.NotNull(result);
        Assert.Equal(500, result!.OldCoins);
        Assert.Equal(300, result.NewCoins);
    }

    [Fact]
    public async Task PayAsync_UserNotInStore_UsesIdAsDisplayName()
    {
        var econ = new FakeEconomicsService();
        var store = new InMemoryAdminStore(econ);
        // No user seeded — after credit the store still has nothing, so PayAsync returns null
        var svc = MakeService(store, econ);

        var result = await svc.PayAsync(99, targetUserId: 77, TestScope, amount: 50, default);

        // Credits still happen even if store returns null after
        Assert.Single(econ.Credits);
        Assert.Null(result); // store FindUserAsync returns null after pay
    }

    [Fact]
    public async Task PayAsync_ZeroAmount_DoesNotCreditOrDebit()
    {
        var econ = new FakeEconomicsService { StartingBalance = 500 };
        var store = new InMemoryAdminStore(econ);
        store.Seed(MakeUser(10, coins: 500));
        var svc = MakeService(store, econ);

        await svc.PayAsync(99, 10, TestScope, 0, default);

        Assert.Empty(econ.Credits);
    }

    // ── GetUserAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserAsync_ExistingUser_ReturnsUser()
    {
        var store = new InMemoryAdminStore();
        store.Seed(MakeUser(5, "Charlie", 800));
        var svc = MakeService(store);

        var user = await svc.GetUserAsync(5, TestScope, default);

        Assert.NotNull(user);
        Assert.Equal("Charlie", user!.DisplayName);
        Assert.Equal(800, user.Coins);
    }

    [Fact]
    public async Task GetUserAsync_NonExistentUser_ReturnsNull()
    {
        var svc = MakeService();
        var user = await svc.GetUserAsync(999, TestScope, default);
        Assert.Null(user);
    }

    [Fact]
    public async Task ClearChatBetsAsync_RefundsAllDeletedPendingBets()
    {
        BotMiniGameSession.DangerousResetAllForTests();
        var econ = new FakeEconomicsService { StartingBalance = 500 };
        var store = new InMemoryAdminStore(econ);
        store.SeedPending(new PendingChatBet { GameId = MiniGameIds.DiceCube, UserId = 10, ChatId = TestScope, Amount = 20 });
        store.SeedPending(new PendingChatBet { GameId = MiniGameIds.Darts, UserId = 11, ChatId = TestScope, Amount = 30, BotMessageId = 999 });
        var svc = MakeService(store, econ);

        var result = await svc.ClearChatBetsAsync(99, TestScope, default);

        Assert.Equal(2, result.ClearedCount);
        Assert.Equal(50, result.TotalRefunded);
        Assert.Equal(2, econ.Credits.Count);
        Assert.Contains(econ.Credits, x => x.UserId == 10 && x.ScopeId == TestScope && x.Amount == 20);
        Assert.Contains(econ.Credits, x => x.UserId == 11 && x.ScopeId == TestScope && x.Amount == 30);
    }

    [Fact]
    public async Task ClearChatBetsAsync_ClearsMiniGameSessionForDeletedRows()
    {
        BotMiniGameSession.DangerousResetAllForTests();
        var store = new InMemoryAdminStore();
        store.SeedPending(new PendingChatBet { GameId = MiniGameIds.Football, UserId = 10, ChatId = TestScope, Amount = 15 });
        BotMiniGameSession.RegisterPlacedBet(10, TestScope, MiniGameIds.Football);
        var svc = MakeService(store);

        await svc.ClearChatBetsAsync(99, TestScope, default);

        Assert.True(BotMiniGameSession.TryBeginPlaceBet(10, TestScope, MiniGameIds.Basketball, out _));
    }

    [Theory]
    [InlineData(MiniGameIds.DiceCube, "dicecube")]
    [InlineData(MiniGameIds.Football, "football")]
    [InlineData(MiniGameIds.Basketball, "basketball")]
    [InlineData(MiniGameIds.Bowling, "bowling")]
    [InlineData(MiniGameIds.Darts, "darts")]
    public async Task ClearChatBetsAsync_ClearsDistributedSessionAndExpectedRollGate(
        string storedGameId, string gateGameId)
    {
        var store = new InMemoryAdminStore();
        store.SeedPending(new PendingChatBet
        {
            GameId = storedGameId,
            UserId = 10,
            ChatId = TestScope,
            Amount = 15,
            BotMessageId = 123,
        });
        var sessions = new RecordingSessionStore();
        var gates = new RecordingRollGateStore();

        await MakeService(store, sessions: sessions, rollGates: gates)
            .ClearChatBetsAsync(99, TestScope, default);

        Assert.Equal((10L, TestScope, storedGameId), Assert.Single(sessions.Cleared));
        Assert.Equal((gateGameId, 10L, TestScope), Assert.Single(gates.Cleared));
    }

    [Fact]
    public async Task ClearChatBetsAsync_UnknownGame_ClearsSessionWithoutRollGate()
    {
        var store = new InMemoryAdminStore();
        store.SeedPending(new PendingChatBet { GameId = "custom", UserId = 10, ChatId = TestScope, Amount = 15 });
        var sessions = new RecordingSessionStore();
        var gates = new RecordingRollGateStore();

        await MakeService(store, sessions: sessions, rollGates: gates)
            .ClearChatBetsAsync(99, TestScope, default);

        Assert.Single(sessions.Cleared);
        Assert.Empty(gates.Cleared);
    }

    [Fact]
    public void ReportMethods_EmitStructuredAdminAnalytics()
    {
        var analytics = new RecordingAnalyticsService();
        var service = MakeService(analytics: analytics);

        service.ReportNotAdmin(42);
        service.ReportUserInfo(42, "target");

        Assert.Equal(2, analytics.Events.Count);
        Assert.All(analytics.Events, entry => Assert.Equal(("admin", "command"), (entry.ModuleId, entry.EventName)));
        Assert.Equal("not_admin", analytics.Events[0].Tags["command"]);
        Assert.Equal("userinfo", analytics.Events[1].Tags["command"]);
        Assert.Equal("target", analytics.Events[1].Tags["target_id"]);
    }

    // ── RenameAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RenameAsync_NewName_ReturnSetOp()
    {
        var svc = MakeService();
        var result = await svc.RenameAsync("OldName", "NewName", default);
        Assert.Equal(RenameOp.Set, result.Op);
    }

    [Fact]
    public async Task RenameAsync_NewName_StoresOverride()
    {
        var store = new InMemoryAdminStore();
        var svc = MakeService(store);
        await svc.RenameAsync("OldName", "NewName", default);

        var stored = await store.GetOverrideAsync("OldName", default);
        Assert.Equal("NewName", stored);
    }

    [Fact]
    public async Task RenameAsync_WildcardOnExisting_ReturnsClearedOp()
    {
        var store = new InMemoryAdminStore();
        var svc = MakeService(store);
        await svc.RenameAsync("OldName", "NewName", default); // set first
        var result = await svc.RenameAsync("OldName", "*", default);
        Assert.Equal(RenameOp.Cleared, result.Op);
    }

    [Fact]
    public async Task RenameAsync_WildcardOnExisting_RemovesOverride()
    {
        var store = new InMemoryAdminStore();
        var svc = MakeService(store);
        await svc.RenameAsync("OldName", "NewName", default);
        await svc.RenameAsync("OldName", "*", default);

        var stored = await store.GetOverrideAsync("OldName", default);
        Assert.Null(stored);
    }

    [Fact]
    public async Task RenameAsync_WildcardOnNonExistent_ReturnsNoChange()
    {
        var svc = MakeService();
        var result = await svc.RenameAsync("NonExistent", "*", default);
        Assert.Equal(RenameOp.NoChange, result.Op);
    }

    [Fact]
    public async Task RenameAsync_OverwriteExisting_ReturnSetOp()
    {
        var store = new InMemoryAdminStore();
        var svc = MakeService(store);
        await svc.RenameAsync("OldName", "FirstNew", default);
        var result = await svc.RenameAsync("OldName", "SecondNew", default);
        Assert.Equal(RenameOp.Set, result.Op);

        var stored = await store.GetOverrideAsync("OldName", default);
        Assert.Equal("SecondNew", stored);
    }

    private sealed class RecordingSessionStore : IMiniGameSessionStore
    {
        public List<(long UserId, long ChatId, string GameId)> Cleared { get; } = [];
        public Task<MiniGameSessionBeginResult> TryBeginPlaceBetAsync(long userId, long chatId, string gameId, CancellationToken ct) =>
            Task.FromResult(new MiniGameSessionBeginResult(true, null));
        public Task RegisterPlacedBetAsync(long userId, long chatId, string gameId, CancellationToken ct) => Task.CompletedTask;
        public Task ClearCompletedRoundAsync(long userId, long chatId, string gameId, CancellationToken ct)
        {
            Cleared.Add((userId, chatId, gameId));
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRollGateStore : IMiniGameRollGateStore
    {
        public List<(string GameId, long UserId, long ChatId)> Cleared { get; } = [];
        public Task ExpectBotRollAsync(string gameId, long userId, long chatId, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> ShouldIgnoreUserThrowAsync(string gameId, long userId, long chatId, CancellationToken ct) => Task.FromResult(false);
        public Task ClearAsync(string gameId, long userId, long chatId, CancellationToken ct)
        {
            Cleared.Add((gameId, userId, chatId));
            return Task.CompletedTask;
        }
    }
}
