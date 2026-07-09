using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System.Text.Json;
using LeaderboardModel = Games.Leaderboard.Domain.Models.Leaderboard;

namespace Games.Leaderboard.Application.Services;

public sealed class LeaderboardService(
    ILeaderboardStore store,
    IEconomicsService economics,
    IAnalyticsService analytics,
    IOptions<LeaderboardOptions> options,
    IConnectionMultiplexer? redis = null) : ILeaderboardService
{
    private readonly LeaderboardOptions _opts = options.Value;
    private static readonly JsonSerializerOptions CacheJson = new(JsonSerializerDefaults.Web);

    public async Task<LeaderboardModel> GetTopAsync(int limit, long balanceScopeId, CancellationToken ct)
    {
        var result = await GetOrCreateCachedAsync(
            $"leaderboard:v1:chat:{balanceScopeId}:{limit}",
            async () =>
            {
                var active = await store.ListActiveAsync(ActiveSinceUnixMs(), balanceScopeId, ct);
                if (active.Count == 0) return new LeaderboardModel([], Truncated: false);

                var places = new List<LeaderboardPlace>();
                var lastBalance = active[0].Coins + 1;
                for (var i = 0; i < active.Count && (places.Count < limit || limit == 0); i++)
                {
                    var user = active[i];
                    if (user.Coins < lastBalance)
                    {
                        lastBalance = user.Coins;
                        places.Add(new LeaderboardPlace(places.Count + 1, []));
                    }
                    places[^1].Users.Add(user);
                }

                var shown = places.Sum(p => p.Users.Count);
                return new LeaderboardModel(places, limit > 0 && places.Count >= limit && active.Count > shown);
            });

        TrackQuery("chat_top", balanceScopeId, limit, result.Places.Sum(p => p.Users.Count), result.Truncated);
        return result;
    }

    public async Task<BalanceInfo> GetBalanceAsync(
        long userId, long balanceScopeId, string displayName, CancellationToken ct)
    {
        await economics.EnsureUserAsync(userId, balanceScopeId, displayName, ct);
        var row = await store.FindAsync(userId, balanceScopeId, ct);
        if (row is not { } r)
        {
            TrackBalance(userId, balanceScopeId, visible: false, found: false);
            return new BalanceInfo(0, Visible: false);
        }

        var visible = r.UpdatedAtUnixMs >= ActiveSinceUnixMs();
        TrackBalance(userId, balanceScopeId, visible, found: true);
        return new BalanceInfo(r.Coins, visible);
    }

    public async Task<GlobalLeaderboard> GetGlobalTopAsync(int limit, CancellationToken ct)
    {
        var result = await GetOrCreateCachedAsync(
            $"leaderboard:v1:global:{limit}",
            async () =>
            {
                var (users, totalUsers) = await store.ListGlobalAggregateAsync(ActiveSinceUnixMs(), limit, ct);
                if (users.Count == 0) return new GlobalLeaderboard([], Truncated: false, TotalUsers: 0);

                var places = new List<GlobalLeaderboardPlace>();
                var lastBalance = users[0].TotalCoins + 1;
                foreach (var user in users)
                {
                    if (user.TotalCoins < lastBalance)
                    {
                        lastBalance = user.TotalCoins;
                        places.Add(new GlobalLeaderboardPlace(places.Count + 1, []));
                    }
                    places[^1].Users.Add(user);
                }

                return new GlobalLeaderboard(places, limit > 0 && totalUsers > users.Count, totalUsers);
            });

        TrackQuery("global_top", 0, limit, result.Places.Sum(p => p.Users.Count), result.Truncated);
        return result;
    }

    public async Task<MultiChatLeaderboard> GetTopByChatAsync(int perChatLimit, CancellationToken ct)
    {
        var result = await GetOrCreateCachedAsync(
            $"leaderboard:v1:split:{perChatLimit}",
            async () =>
            {
                var rows = await store.ListGlobalSplitAsync(ActiveSinceUnixMs(), perChatLimit, ct);
                if (rows.Count == 0) return new MultiChatLeaderboard([]);

                var grouped = rows
                    .GroupBy(r => r.ChatId)
                    .Select(g =>
                    {
                        var first = g.First();
                        var places = BuildPlaces(g.Select(x => x.User).ToList());
                        return (
                            Chat: new ChatLeaderboard(first.ChatId, first.Title, first.ChatType, places, Truncated: false),
                            TopCoins: g.Max(x => x.User.Coins));
                    })
                    .OrderByDescending(x => x.TopCoins)
                    .ThenBy(x => x.Chat.ChatId)
                    .Select(x => x.Chat)
                    .ToList();

                return new MultiChatLeaderboard(grouped);
            });

        TrackQuery("split_top", 0, perChatLimit, result.Chats.Sum(c => c.Places.Sum(p => p.Users.Count)), false, result.Chats.Count);
        return result;
    }

    private void TrackQuery(
        string mode, long balanceScopeId, int limit, int usersReturned, bool truncated, int? chatCount = null)
    {
        var tags = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["mode"] = mode,
            ["balance_scope_id"] = balanceScopeId,
            ["limit"] = limit,
            ["users_returned"] = usersReturned,
            ["truncated"] = truncated,
            ["outcome"] = usersReturned == 0 ? "empty" : "success",
        };
        if (chatCount is not null) tags["chat_count"] = chatCount.Value;
        analytics.Track("leaderboard", "viewed", tags);
    }

    private void TrackBalance(long userId, long balanceScopeId, bool visible, bool found) =>
        analytics.Track("leaderboard", "balance_viewed", new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["user_id"] = userId,
            ["balance_scope_id"] = balanceScopeId,
            ["visible"] = visible,
            ["found"] = found,
            ["outcome"] = found ? "success" : "not_found",
        });

    private long ActiveSinceUnixMs()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return now - ((long)_opts.DaysOfInactivityToHide * 24 * 60 * 60 * 1000);
    }

    private async Task<T> GetOrCreateCachedAsync<T>(string key, Func<Task<T>> factory)
    {
        if (redis is null) return await factory();

        try
        {
            var database = redis.GetDatabase();
            var cached = await database.StringGetAsync(key);
            if (cached.HasValue)
            {
                var value = JsonSerializer.Deserialize<T>(cached.ToString(), CacheJson);
                if (value is not null) return value;
            }

            var fresh = await factory();
            await database.StringSetAsync(
                key,
                JsonSerializer.Serialize(fresh, CacheJson),
                TimeSpan.FromSeconds(Math.Clamp(_opts.CacheSeconds, 1, 300)));
            return fresh;
        }
        catch (RedisException)
        {
            // A leaderboard is a read model: Redis must never make it unavailable.
            return await factory();
        }
    }

    private static List<LeaderboardPlace> BuildPlaces(List<LeaderboardUser> sortedDesc)
    {
        var places = new List<LeaderboardPlace>();
        if (sortedDesc.Count == 0) return places;

        var lastBalance = sortedDesc[0].Coins + 1;
        foreach (var user in sortedDesc)
        {
            if (user.Coins < lastBalance)
            {
                lastBalance = user.Coins;
                places.Add(new LeaderboardPlace(places.Count + 1, []));
            }
            places[^1].Users.Add(user);
        }
        return places;
    }
}
