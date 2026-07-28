namespace Games.Leaderboard.Domain.Models;

public sealed record GlobalLeaderboard(IReadOnlyList<GlobalLeaderboardPlace> Places, bool Truncated, int TotalUsers);
