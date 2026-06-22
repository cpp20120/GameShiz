namespace Games.Meta;

public sealed record MetaSeason(
    long Id,
    string Name,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    string Status,
    string ConfigJson);
