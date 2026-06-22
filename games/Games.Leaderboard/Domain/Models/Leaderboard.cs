namespace Games.Leaderboard.Domain.Models;

public sealed record Leaderboard(List<LeaderboardPlace> Places, bool Truncated);
