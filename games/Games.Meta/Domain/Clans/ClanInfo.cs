namespace Games.Meta;

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
