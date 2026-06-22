namespace Games.Leaderboard;

public sealed record GlobalLeaderboardPlace(int Place, List<GlobalLeaderboardUser> Users);
