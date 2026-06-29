using Microsoft.Extensions.Options;
using LeaderboardModel = Games.Leaderboard.Domain.Models.Leaderboard;

namespace Games.Leaderboard.Application.Services;

public sealed class LeaderboardService(
    ILeaderboardStore store,
    IEconomicsService economics,
    IAnalyticsService analytics,
    IOptions<LeaderboardOptions> options) : ILeaderboardService
{
    private readonly LeaderboardOptions _opts = options.Value;

    public async Task<LeaderboardModel> GetTopAsync(int limit, long balanceScopeId, CancellationToken ct)
    {
        var since = ActiveSinceUnixMs();

        var active = await store.ListActiveAsync(since, balanceScopeId, ct);
        if (active.Count == 0)
        {
            TrackQuery("chat_top", balanceScopeId, limit, 0, false);
            return new LeaderboardModel([], Truncated: false);
        }

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
        var truncated = limit > 0 && places.Count >= limit && active.Count > shown;
        TrackQuery("chat_top", balanceScopeId, limit, shown, truncated);
        return new LeaderboardModel(places, truncated);
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
        var since = ActiveSinceUnixMs();
        var (users, totalUsers) = await store.ListGlobalAggregateAsync(since, limit, ct);

        if (users.Count == 0)
        {
            TrackQuery("global_top", 0, limit, 0, false);
            return new GlobalLeaderboard([], Truncated: false, TotalUsers: 0);
        }

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

        var truncated = limit > 0 && totalUsers > users.Count;
        TrackQuery("global_top", 0, limit, users.Count, truncated);
        return new GlobalLeaderboard(places, truncated, totalUsers);
    }

    public async Task<MultiChatLeaderboard> GetTopByChatAsync(int perChatLimit, CancellationToken ct)
    {
        var since = ActiveSinceUnixMs();
        var rows = await store.ListGlobalSplitAsync(since, perChatLimit, ct);
        if (rows.Count == 0)
        {
            TrackQuery("split_top", 0, perChatLimit, 0, false, chatCount: 0);
            return new MultiChatLeaderboard([]);
        }

        var grouped = rows
            .GroupBy(r => r.ChatId)
            .Select(g =>
            {
                var first = g.First();
                var places = BuildPlaces(g.Select(x => x.User).ToList());
                return (
                    Chat: new ChatLeaderboard(
                        ChatId: first.ChatId,
                        Title: first.Title,
                        ChatType: first.ChatType,
                        Places: places,
                        Truncated: false),
                    TopCoins: g.Max(x => x.User.Coins));
            })
            .OrderByDescending(x => x.TopCoins)
            .ThenBy(x => x.Chat.ChatId)
            .Select(x => x.Chat)
            .ToList();

        TrackQuery("split_top", 0, perChatLimit, rows.Count, false, grouped.Count);
        return new MultiChatLeaderboard(grouped);
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

    private static List<LeaderboardPlace> BuildPlaces(IReadOnlyList<LeaderboardUser> sortedDesc)
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
