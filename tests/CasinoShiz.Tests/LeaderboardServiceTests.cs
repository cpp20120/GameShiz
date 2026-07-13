using System.Globalization;
using BotFramework.Contracts.Caching;
using Microsoft.Extensions.Options;
using Xunit;

namespace CasinoShiz.Tests;

public class LeaderboardServiceTests
{
    private const long ChatScope = 100;
    private static readonly long NowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    private static readonly long RecentMs = NowMs - 1_000;           // 1 second ago — active
    private static readonly long StaleMs = NowMs - (10 * 24 * 3600 * 1_000L); // 10 days ago — inactive (> 7 day default)

    private static LeaderboardService MakeService(
        InMemoryLeaderboardStore? store = null,
        FakeEconomicsService? economics = null,
        IAnalyticsService? analytics = null,
        int daysToHide = 7) =>
        new(
            store ?? new InMemoryLeaderboardStore(),
            economics ?? new FakeEconomicsService(),
            analytics ?? new NullAnalyticsService(),
            Options.Create(new LeaderboardOptions { DaysOfInactivityToHide = daysToHide }));

    // ── GetTopAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTopAsync_Empty_ReturnsEmptyLeaderboard()
    {
        var svc = MakeService();
        var lb = await svc.GetTopAsync(10, ChatScope, default);
        Assert.Empty(lb.Places);
        Assert.False(lb.Truncated);
    }

    [Fact]
    public async Task GetTopAsync_DistributedCache_AvoidsSecondReadModelQuery()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, ChatScope, "Alice", 500, RecentMs);
        var cache = new RecordingCacheStore();
        var service = new LeaderboardService(
            store,
            new FakeEconomicsService(),
            new NullAnalyticsService(),
            Options.Create(new LeaderboardOptions()),
            cache);

        await service.GetTopAsync(10, ChatScope, default);
        await service.GetTopAsync(10, ChatScope, default);

        Assert.Equal(1, store.ListActiveCallCount);
        Assert.Single(cache.Values);
    }

    [Fact]
    public async Task GetTopAsync_ActiveUsers_ReturnsSortedByCoins()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, ChatScope, "Alice", 500, RecentMs);
        store.Seed(2, ChatScope, "Bob", 300, RecentMs);
        store.Seed(3, ChatScope, "Carol", 900, RecentMs);
        var svc = MakeService(store);

        var lb = await svc.GetTopAsync(10, ChatScope, default);

        var coins = lb.Places.SelectMany(p => p.Users).Select(u => u.Coins).ToList();
        Assert.Equal([900, 500, 300], coins);
    }

    [Fact]
    public async Task GetTopAsync_InactiveUsers_AreFiltered()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, ChatScope, "Active", 500, RecentMs);
        store.Seed(2, ChatScope, "Inactive", 9999, StaleMs); // high coins but stale
        var svc = MakeService(store);

        var lb = await svc.GetTopAsync(10, ChatScope, default);

        var allUsers = lb.Places.SelectMany(p => p.Users).ToList();
        Assert.DoesNotContain(allUsers, u => string.Equals(u.DisplayName, "Inactive", StringComparison.Ordinal));
        Assert.Single(allUsers);
    }

    [Fact]
    public async Task GetTopAsync_UsersWithSameCoins_GroupedInSamePlace()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, ChatScope, "Alice", 500, RecentMs);
        store.Seed(2, ChatScope, "Bob", 500, RecentMs);
        store.Seed(3, ChatScope, "Carol", 300, RecentMs);
        var svc = MakeService(store);

        var lb = await svc.GetTopAsync(10, ChatScope, default);

        Assert.Equal(2, lb.Places.Count);
        Assert.Equal(2, lb.Places[0].Users.Count); // Alice and Bob tied at rank 1
        Assert.Single(lb.Places[1].Users);          // Carol at rank 2
    }

    [Fact]
    public async Task GetTopAsync_PlaceNumbers_AreCorrect()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, ChatScope, "Alice", 500, RecentMs);
        store.Seed(2, ChatScope, "Bob", 300, RecentMs);
        var svc = MakeService(store);

        var lb = await svc.GetTopAsync(10, ChatScope, default);

        Assert.Equal(1, lb.Places[0].Place);
        Assert.Equal(2, lb.Places[1].Place);
    }

    [Fact]
    public async Task GetTopAsync_LimitReached_SetsTruncated()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, ChatScope, "A", 900, RecentMs);
        store.Seed(2, ChatScope, "B", 800, RecentMs);
        store.Seed(3, ChatScope, "C", 700, RecentMs);
        var svc = MakeService(store);

        // limit=2, but there are 3 users → truncated
        var lb = await svc.GetTopAsync(2, ChatScope, default);

        Assert.True(lb.Truncated);
    }

    [Fact]
    public async Task GetTopAsync_LimitNotReached_NotTruncated()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, ChatScope, "A", 500, RecentMs);
        store.Seed(2, ChatScope, "B", 300, RecentMs);
        var svc = MakeService(store);

        var lb = await svc.GetTopAsync(10, ChatScope, default);

        Assert.False(lb.Truncated);
    }

    [Fact]
    public async Task GetTopAsync_ZeroLimit_ReturnsAll()
    {
        var store = new InMemoryLeaderboardStore();
        for (var i = 0; i < 20; i++)
            store.Seed(i, ChatScope, string.Create(CultureInfo.InvariantCulture, $"U{i}"), 100 - i, RecentMs);
        var svc = MakeService(store);

        var lb = await svc.GetTopAsync(limit: 0, ChatScope, default);

        Assert.Equal(20, lb.Places.Count);
        Assert.False(lb.Truncated);
    }

    [Fact]
    public async Task GetTopAsync_DaysToHide_ControlsCutoff()
    {
        var store = new InMemoryLeaderboardStore();
        // User active 2 days ago — hidden with daysToHide=1, visible with daysToHide=3
        var twoDaysAgoMs = NowMs - (2 * 24 * 3600 * 1_000L);
        store.Seed(1, ChatScope, "U1", 500, twoDaysAgoMs);
        var svc1 = MakeService(store, daysToHide: 1);
        var svc3 = MakeService(store, daysToHide: 3);

        var lb1 = await svc1.GetTopAsync(10, ChatScope, default);
        var lb3 = await svc3.GetTopAsync(10, ChatScope, default);

        Assert.Empty(lb1.Places);
        Assert.Single(lb3.Places);
    }

    // ── GetBalanceAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetBalanceAsync_UserInStore_ReturnsCoins()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, ChatScope, "Alice", 750, RecentMs);
        var svc = MakeService(store);

        var info = await svc.GetBalanceAsync(1, ChatScope, "Alice", default);

        Assert.Equal(750, info.Coins);
    }

    [Fact]
    public async Task GetBalanceAsync_ActiveUser_IsVisible()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, ChatScope, "Alice", 750, RecentMs);
        var svc = MakeService(store);

        var info = await svc.GetBalanceAsync(1, ChatScope, "Alice", default);

        Assert.True(info.Visible);
    }

    [Fact]
    public async Task GetBalanceAsync_InactiveUser_IsNotVisible()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, ChatScope, "Alice", 750, StaleMs);
        var svc = MakeService(store);

        var info = await svc.GetBalanceAsync(1, ChatScope, "Alice", default);

        Assert.False(info.Visible);
    }

    [Fact]
    public async Task GetBalanceAsync_UserNotInStore_ReturnsZeroInvisible()
    {
        var svc = MakeService();

        var info = await svc.GetBalanceAsync(42, ChatScope, "Unknown", default);

        Assert.Equal(0, info.Coins);
        Assert.False(info.Visible);
    }

    // ── Global leaderboards ─────────────────────────────────────────────────

    [Fact]
    public async Task GetGlobalTopAsync_Empty_ReturnsEmptyResult()
    {
        var result = await MakeService().GetGlobalTopAsync(10, default);

        Assert.Empty(result.Places);
        Assert.Equal(0, result.TotalUsers);
        Assert.False(result.Truncated);
    }

    [Fact]
    public async Task GetGlobalTopAsync_AggregatesBalancesAcrossChatsAndGroupsTies()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, 10, "Alice", 300, RecentMs);
        store.Seed(1, 20, "Alice", 200, RecentMs);
        store.Seed(2, 10, "Bob", 500, RecentMs);
        store.Seed(3, 20, "Carol", 100, RecentMs);

        var result = await MakeService(store).GetGlobalTopAsync(10, default);

        Assert.Equal(3, result.TotalUsers);
        Assert.Equal(2, result.Places.Count);
        Assert.Equal(2, result.Places[0].Users.Count);
        Assert.All(result.Places[0].Users, user => Assert.Equal(500, user.TotalCoins));
        Assert.Equal(2, result.Places[0].Users.Single(user => user.TelegramUserId == 1).ChatCount);
    }

    [Fact]
    public async Task GetGlobalTopAsync_LimitSetsTruncatedAndTotalCount()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, 10, "A", 300, RecentMs);
        store.Seed(2, 10, "B", 200, RecentMs);
        store.Seed(3, 10, "C", 100, RecentMs);

        var result = await MakeService(store).GetGlobalTopAsync(2, default);

        Assert.True(result.Truncated);
        Assert.Equal(3, result.TotalUsers);
        Assert.Equal(2, result.Places.Count);
    }

    [Fact]
    public async Task GetTopByChatAsync_Empty_ReturnsNoChats()
    {
        var result = await MakeService().GetTopByChatAsync(10, default);

        Assert.Empty(result.Chats);
    }

    [Fact]
    public async Task GetTopByChatAsync_GroupsRanksAndOrdersChatsByTopBalance()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, 10, "A", 100, RecentMs);
        store.Seed(2, 10, "B", 100, RecentMs);
        store.Seed(3, 20, "C", 500, RecentMs);
        store.Seed(4, 20, "D", 50, RecentMs);

        var result = await MakeService(store).GetTopByChatAsync(10, default);

        Assert.Equal([20L, 10L], result.Chats.Select(chat => chat.ChatId).ToArray());
        Assert.Equal(2, result.Chats[0].Places.Count);
        Assert.Single(result.Chats[1].Places);
        Assert.Equal(2, result.Chats[1].Places[0].Users.Count);
    }

    [Fact]
    public async Task GetTopByChatAsync_PerChatLimitRestrictsEachChat()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, 10, "A", 300, RecentMs);
        store.Seed(2, 10, "B", 200, RecentMs);
        store.Seed(3, 20, "C", 100, RecentMs);
        store.Seed(4, 20, "D", 50, RecentMs);

        var result = await MakeService(store).GetTopByChatAsync(1, default);

        Assert.All(result.Chats, chat => Assert.Single(chat.Places.SelectMany(place => place.Users)));
    }

    [Fact]
    public async Task Queries_EmitStructuredAnalytics()
    {
        var store = new InMemoryLeaderboardStore();
        store.Seed(1, ChatScope, "Alice", 500, RecentMs);
        var analytics = new RecordingAnalyticsService();
        var svc = MakeService(store, analytics: analytics);

        await svc.GetTopAsync(15, ChatScope, default);
        await svc.GetBalanceAsync(1, ChatScope, "Alice", default);
        await svc.GetGlobalTopAsync(30, default);
        await svc.GetTopByChatAsync(5, default);

        Assert.Equal(4, analytics.Events.Count);
        Assert.Contains(analytics.Events, x => x.EventName == "viewed" && Equals(x.Tags["mode"], "chat_top"));
        Assert.Contains(analytics.Events, x => x.EventName == "viewed" && Equals(x.Tags["mode"], "global_top"));
        Assert.Contains(analytics.Events, x => x.EventName == "viewed" && Equals(x.Tags["mode"], "split_top"));
        Assert.Contains(analytics.Events, x => x.EventName == "balance_viewed" && Equals(x.Tags["found"], true));
        Assert.All(analytics.Events, x => Assert.Equal("leaderboard", x.ModuleId));
    }
}

file sealed class RecordingCacheStore : ICacheStore
{
    public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);

    public Task<string?> GetStringAsync(string key, CancellationToken ct) =>
        Task.FromResult(Values.GetValueOrDefault(key));

    public Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken ct)
    {
        Values[key] = value;
        return Task.CompletedTask;
    }
}
