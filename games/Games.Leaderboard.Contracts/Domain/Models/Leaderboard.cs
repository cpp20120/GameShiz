namespace Games.Leaderboard.Domain.Models;

public sealed record Leaderboard(IReadOnlyList<LeaderboardPlace> Places, bool Truncated);
