namespace Games.Leaderboard;

public sealed record ChatLeaderboard(
    long ChatId,
    string? Title,
    string ChatType,
    List<LeaderboardPlace> Places,
    bool Truncated);
