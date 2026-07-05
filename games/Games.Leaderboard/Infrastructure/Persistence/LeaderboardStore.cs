using Dapper;

namespace Games.Leaderboard.Infrastructure.Persistence;

public sealed class LeaderboardStore(INpgsqlConnectionFactory connections, IWalletReadService wallets) : ILeaderboardStore
{
    public async Task<IReadOnlyList<LeaderboardUser>> ListActiveAsync(
        long sinceUnixMs, long balanceScopeId, CancellationToken ct)
    {
        var rows = await wallets.ListByScopeAsync(balanceScopeId, ct);
        return rows.Where(x => x.UpdatedAt.ToUnixTimeMilliseconds() >= sinceUnixMs)
            .OrderByDescending(x => x.Coins).Select(Map).ToList();
    }

    public async Task<(int Coins, long UpdatedAtUnixMs)?> FindAsync(
        long userId, long balanceScopeId, CancellationToken ct)
    {
        var row = await wallets.GetAsync(userId, balanceScopeId, ct);
        return row is null ? null : (row.Coins, row.UpdatedAt.ToUnixTimeMilliseconds());
    }

    public async Task<(IReadOnlyList<GlobalLeaderboardUser> Users, int TotalUsers)> ListGlobalAggregateAsync(
        long sinceUnixMs, int limit, CancellationToken ct)
    {
        var accounts = await wallets.ListAsync(ct);
        var all = accounts.Where(x => x.UpdatedAt.ToUnixTimeMilliseconds() >= sinceUnixMs)
            .GroupBy(x => x.UserId)
            .Select(group => new GlobalLeaderboardUser(
                group.Key,
                group.OrderByDescending(x => x.UpdatedAt).First().DisplayName,
                group.Sum(x => x.Coins),
                group.Count()))
            .OrderByDescending(x => x.TotalCoins).ThenBy(x => x.TelegramUserId).ToList();

        if (limit > 0 && all.Count > limit)
            return (all.Take(limit).ToList(), all.Count);

        return (all, all.Count);
    }

    public async Task<IReadOnlyList<(long ChatId, string? Title, string ChatType, LeaderboardUser User)>>
        ListGlobalSplitAsync(long sinceUnixMs, int perChatLimit, CancellationToken ct)
    {
        var accounts = await wallets.ListAsync(ct);
        await using var conn = await connections.OpenAsync(ct);
        var chats = (await conn.QueryAsync<ChatInfo>(new CommandDefinition(
            "SELECT chat_id AS ChatId, title AS Title, COALESCE(chat_type, 'unknown') AS ChatType FROM known_chats",
            cancellationToken: ct))).ToDictionary(x => x.ChatId);
        return accounts.Where(x => x.UpdatedAt.ToUnixTimeMilliseconds() >= sinceUnixMs)
            .GroupBy(x => x.BalanceScopeId).OrderBy(x => x.Key)
            .SelectMany(group => (perChatLimit > 0
                    ? group.OrderByDescending(x => x.Coins).ThenBy(x => x.UserId).Take(perChatLimit)
                    : group.OrderByDescending(x => x.Coins).ThenBy(x => x.UserId))
                .Select(account => (
                    group.Key,
                    chats.GetValueOrDefault(group.Key)?.Title,
                    chats.GetValueOrDefault(group.Key)?.ChatType ?? "unknown",
                    Map(account))))
            .ToList();
    }

    private static LeaderboardUser Map(WalletAccount account) => new(
        account.UserId, account.BalanceScopeId, account.DisplayName, account.Coins,
        account.UpdatedAt.ToUnixTimeMilliseconds());

    private sealed record ChatInfo(long ChatId, string? Title, string ChatType);
}
