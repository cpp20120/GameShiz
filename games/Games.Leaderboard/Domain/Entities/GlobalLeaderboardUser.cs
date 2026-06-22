namespace Games.Leaderboard;

public sealed record GlobalLeaderboardUser(
    long TelegramUserId, string DisplayName, int TotalCoins, int ChatCount);
