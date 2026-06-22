namespace Games.Leaderboard;

public sealed record Leaderboard(List<LeaderboardPlace> Places, bool Truncated);
