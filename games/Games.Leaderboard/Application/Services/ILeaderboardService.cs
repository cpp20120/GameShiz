using BotFramework.Host;
using Microsoft.Extensions.Options;

namespace Games.Leaderboard;

public interface ILeaderboardService
{
    Task<Leaderboard> GetTopAsync(int limit, long balanceScopeId, CancellationToken ct);
    Task<BalanceInfo> GetBalanceAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct);
    Task<GlobalLeaderboard> GetGlobalTopAsync(int limit, CancellationToken ct);
    Task<MultiChatLeaderboard> GetTopByChatAsync(int perChatLimit, CancellationToken ct);
}
