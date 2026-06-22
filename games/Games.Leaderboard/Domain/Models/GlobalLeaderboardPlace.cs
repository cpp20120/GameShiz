namespace Games.Leaderboard.Domain.Models;

public sealed record GlobalLeaderboardPlace(int Place, List<GlobalLeaderboardUser> Users);
