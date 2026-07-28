namespace Games.Leaderboard.Domain.Models;

public sealed record ChatLeaderboard(
    long ChatId,
    string? Title,
    string ChatType,
    IReadOnlyList<LeaderboardPlace> Places,
    bool Truncated);
