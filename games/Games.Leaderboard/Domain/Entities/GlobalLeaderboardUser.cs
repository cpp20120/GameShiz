namespace Games.Leaderboard.Domain.Entities;

public sealed record GlobalLeaderboardUser(
    long TelegramUserId, string DisplayName, int TotalCoins, int ChatCount);
