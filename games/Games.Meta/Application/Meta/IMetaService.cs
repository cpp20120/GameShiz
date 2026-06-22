namespace Games.Meta;

public interface IMetaService
{
    Task<MetaSeason> GetActiveSeasonAsync(CancellationToken ct);
    Task<SeasonProfile> GetProfileAsync(long chatId, long userId, string displayName, CancellationToken ct);
    Task<IReadOnlyList<SeasonLeaderboardEntry>> GetTopAsync(long chatId, int limit, CancellationToken ct);
    Task<IReadOnlyList<PlayerAchievementView>> GetAchievementsAsync(long chatId, long userId, CancellationToken ct);
    Task<GameStreakRecordResult?> RecordGamePlayedAsync(
        long seasonId,
        long chatId,
        long userId,
        string gameKey,
        DateOnly playedOn,
        CancellationToken ct);
    Task<IReadOnlyList<PlayerGameStreakView>> GetGameStreaksAsync(long chatId, long userId, CancellationToken ct);
    Task<SeasonPlayer> ApplyGameCompletedAsync(
        long chatId,
        long userId,
        string displayName,
        long stake,
        long payout,
        bool isWin,
        CancellationToken ct);
    Task<SeasonPlayer> AddSeasonXpAsync(
        long seasonId,
        long chatId,
        long userId,
        string displayName,
        long xpDelta,
        CancellationToken ct);
    Task<IReadOnlyList<AchievementUnlock>> UnlockAchievementsAsync(
        long seasonId,
        long chatId,
        long userId,
        IEnumerable<AchievementDefinition> achievements,
        CancellationToken ct);
}
