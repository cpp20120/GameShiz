namespace Games.Leaderboard.Domain.Models;

public sealed record GlobalLeaderboard(List<GlobalLeaderboardPlace> Places, bool Truncated, int TotalUsers);
