namespace Games.Leaderboard;

public sealed record LeaderboardUser(
    long TelegramUserId, long BalanceScopeId, string DisplayName, int Coins, long UpdatedAtUnixMs);
