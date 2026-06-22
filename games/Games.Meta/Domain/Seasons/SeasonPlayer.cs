namespace Games.Meta.Domain.Seasons;

public sealed record SeasonPlayer(
    long SeasonId,
    long ChatId,
    long UserId,
    string DisplayName,
    long Xp,
    int Level,
    int Rating,
    int GamesPlayed,
    int Wins,
    int Losses,
    long TotalStaked,
    long TotalPayout,
    DateTimeOffset UpdatedAt);
