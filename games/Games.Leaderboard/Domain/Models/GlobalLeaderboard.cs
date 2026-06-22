namespace Games.Leaderboard;

public sealed record GlobalLeaderboard(List<GlobalLeaderboardPlace> Places, bool Truncated, int TotalUsers);
