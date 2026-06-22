namespace Games.Meta.Domain.Seasons;

public sealed record MetaSeason(
    long Id,
    string Name,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Status,
    string ConfigJson);
