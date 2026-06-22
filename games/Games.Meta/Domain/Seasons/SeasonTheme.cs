namespace Games.Meta.Domain.Seasons;

internal sealed record SeasonTheme(
    string Key,
    string Title,
    int PlayXp,
    int WinXp,
    int LossXp,
    decimal StakeMultiplier,
    int MaxXpPerGame,
    int WinRatingDelta,
    int LossRatingDelta,
    int MaxClanMembers,
    int MaxActiveTournamentsPerChat,
    int LargeWinMultiplierAlert,
    int SuspiciousStreakThreshold,
    string QuestFocus,
    string RarityBias);
