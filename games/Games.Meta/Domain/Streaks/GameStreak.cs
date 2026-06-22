namespace Games.Meta;

public sealed record GameStreak(
    long SeasonId,
    long ChatId,
    long UserId,
    string GameKey,
    int CurrentStreak,
    int BestStreak,
    int TotalPlayDays,
    DateOnly LastPlayedOn,
    DateTimeOffset UpdatedAt);
