namespace Games.Meta.Domain.Achievements;

public sealed record AchievementUnlock(
    string AchievementId,
    long SeasonId,
    long ChatId,
    long UserId,
    DateTimeOffset UnlockedAt);
