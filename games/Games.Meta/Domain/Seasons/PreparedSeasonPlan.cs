namespace Games.Meta;

public sealed record PreparedSeasonPlan(
    string Name,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string ConfigJson);
