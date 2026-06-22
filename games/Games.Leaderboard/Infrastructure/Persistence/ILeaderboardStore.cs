using BotFramework.Host;
using Dapper;

namespace Games.Leaderboard;

public interface ILeaderboardStore
{
    Task<IReadOnlyList<LeaderboardUser>> ListActiveAsync(long sinceUnixMs, long balanceScopeId, CancellationToken ct);
    Task<(int Coins, long UpdatedAtUnixMs)?> FindAsync(long userId, long balanceScopeId, CancellationToken ct);
    Task<(IReadOnlyList<GlobalLeaderboardUser> Users, int TotalUsers)> ListGlobalAggregateAsync(
        long sinceUnixMs, int limit, CancellationToken ct);
    Task<IReadOnlyList<(long ChatId, string? Title, string ChatType, LeaderboardUser User)>> ListGlobalSplitAsync(
        long sinceUnixMs, int perChatLimit, CancellationToken ct);
}
