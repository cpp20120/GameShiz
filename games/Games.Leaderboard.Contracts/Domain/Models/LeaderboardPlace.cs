namespace Games.Leaderboard.Domain.Models;

public sealed record LeaderboardPlace(int Place, IReadOnlyList<LeaderboardUser> Users);
