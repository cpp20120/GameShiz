namespace Games.Meta.Infrastructure.Persistence;

internal sealed record GameStreakRow(
    string GameKey,
    int CurrentStreak,
    int BestStreak,
    int TotalPlayDays,
    DateOnly LastPlayedOn);
