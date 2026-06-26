using Microsoft.Extensions.Options;
using LeaderboardModel = Games.Leaderboard.Domain.Models.Leaderboard;

namespace Games.Leaderboard.Application.Services;

public interface ILeaderboardService
{
    Task<LeaderboardModel> GetTopAsync(int limit, long balanceScopeId, CancellationToken ct);
    Task<BalanceInfo> GetBalanceAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct);
    Task<GlobalLeaderboard> GetGlobalTopAsync(int limit, CancellationToken ct);
    Task<MultiChatLeaderboard> GetTopByChatAsync(int perChatLimit, CancellationToken ct);
}
