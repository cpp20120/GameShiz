namespace Games.Meta.Domain.Seasons;

public sealed record PreparedSeasonPlan(
    string Name,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string ConfigJson);
