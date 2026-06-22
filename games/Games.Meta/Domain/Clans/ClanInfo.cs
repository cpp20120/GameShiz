namespace Games.Meta.Domain.Clans;

public sealed record ClanInfo(
    long Id,
    long ChatId,
    string Name,
    string Tag,
    long OwnerUserId,
    DateTimeOffset CreatedAt,
    int MemberCount,
    long SeasonXp,
    int SeasonRating);
