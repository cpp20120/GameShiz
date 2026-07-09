namespace Games.Leaderboard.Domain.Configuration;

public sealed class LeaderboardOptions
{
    public const string SectionName = "Games:leaderboard";

    public int DefaultLimit { get; init; } = 15;

    public int DaysOfInactivityToHide { get; init; } = 7;

    /// <summary>
    /// Short distributed-cache lifetime for public leaderboard read models.
    /// Balance reads deliberately bypass this cache.
    /// </summary>
    public int CacheSeconds { get; init; } = 20;
}
