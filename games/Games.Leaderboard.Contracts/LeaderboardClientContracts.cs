namespace Games.Leaderboard.Contracts;

public enum DailyClaimStatus
{
    Claimed,
    AlreadyClaimedToday,
    Disabled,
    IneligibleEmptyBalance,
    IneligiblePercentRoundsToZero,
}

public sealed record DailyClaimResponse(DailyClaimStatus Status, int BonusCoins, int NewBalance);

public interface ILeaderboardClient
{
    Task<global::Games.Leaderboard.Domain.Models.Leaderboard> GetTopAsync(int limit, long balanceScopeId, CancellationToken ct);
    Task<BalanceInfo> GetBalanceAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct);
    Task<GlobalLeaderboard> GetGlobalTopAsync(int limit, CancellationToken ct);
    Task<MultiChatLeaderboard> GetTopByChatAsync(int perChatLimit, CancellationToken ct);
    Task<DailyClaimResponse> ClaimDailyAsync(
        long userId, long balanceScopeId, string displayName, CancellationToken ct);
}
