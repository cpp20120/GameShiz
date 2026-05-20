namespace Games.Meta;

public sealed record MetaSeason(
    long Id,
    string Name,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Status,
    string ConfigJson);

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

public sealed record SeasonProfile(
    MetaSeason Season,
    SeasonPlayer Player,
    string Division,
    long NextLevelXp,
    long CurrentLevelXpFloor);

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
