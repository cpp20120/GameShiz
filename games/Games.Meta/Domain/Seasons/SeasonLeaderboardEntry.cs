namespace Games.Meta.Domain.Seasons;

public sealed record SeasonLeaderboardEntry(
    int Place,
    long UserId,
    string DisplayName,
    long Xp,
    int Level,
    int Rating,
    int GamesPlayed,
    int Wins,
    int Losses);
