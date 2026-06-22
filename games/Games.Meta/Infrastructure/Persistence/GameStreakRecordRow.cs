namespace Games.Meta;

internal sealed record GameStreakRecordRow(
    long SeasonId,
    long ChatId,
    long UserId,
    string GameKey,
    int CurrentStreak,
    int BestStreak,
    int TotalPlayDays,
    DateOnly LastPlayedOn,
    DateTimeOffset UpdatedAt,
    bool Advanced);
