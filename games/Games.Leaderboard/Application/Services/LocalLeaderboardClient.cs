using Games.Leaderboard.Contracts;

namespace Games.Leaderboard.Application.Services;

public sealed class LocalLeaderboardClient(
    ILeaderboardService leaderboard,
    IDailyBonusService dailyBonus) : ILeaderboardClient
{
    public Task<global::Games.Leaderboard.Domain.Models.Leaderboard> GetTopAsync(int limit, long balanceScopeId, CancellationToken ct) =>
        leaderboard.GetTopAsync(limit, balanceScopeId, ct);
    public Task<BalanceInfo> GetBalanceAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct) =>
        leaderboard.GetBalanceAsync(userId, balanceScopeId, displayName, ct);
    public Task<GlobalLeaderboard> GetGlobalTopAsync(int limit, CancellationToken ct) =>
        leaderboard.GetGlobalTopAsync(limit, ct);
    public Task<MultiChatLeaderboard> GetTopByChatAsync(int perChatLimit, CancellationToken ct) =>
        leaderboard.GetTopByChatAsync(perChatLimit, ct);

    public async Task<DailyClaimResponse> ClaimDailyAsync(
        long userId, long balanceScopeId, string displayName, CancellationToken ct)
    {
        var result = await dailyBonus.TryClaimAsync(userId, balanceScopeId, displayName, ct);
        return new DailyClaimResponse((DailyClaimStatus)result.Status, result.BonusCoins, result.NewBalance);
    }
}
