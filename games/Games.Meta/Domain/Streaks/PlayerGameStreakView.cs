namespace Games.Meta;

public sealed record PlayerGameStreakView(
    string GameKey,
    string Title,
    string Command,
    int CurrentStreak,
    int BestStreak,
    int TotalPlayDays,
    DateOnly? LastPlayedOn);
