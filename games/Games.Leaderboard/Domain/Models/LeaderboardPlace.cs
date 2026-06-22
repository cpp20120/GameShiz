namespace Games.Leaderboard;

public sealed record LeaderboardPlace(int Place, List<LeaderboardUser> Users);
