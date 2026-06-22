namespace Games.Meta.Domain.Achievements;

public sealed record AchievementDefinition(
    string Id,
    string Title,
    string Description,
    string Category,
    bool IsSeasonal,
    bool IsSecret);
