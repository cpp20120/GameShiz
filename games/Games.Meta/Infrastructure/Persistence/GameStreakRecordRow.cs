namespace Games.Meta.Infrastructure.Persistence;

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
