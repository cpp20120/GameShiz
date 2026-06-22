namespace Games.Meta.Domain.Achievements;

public sealed record PlayerAchievementView(
    string Id,
    string Title,
    string Description,
    string Category,
    bool IsUnlocked,
    DateTimeOffset? UnlockedAt);
