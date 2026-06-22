namespace Games.Meta.Domain.Clans;

public sealed record ClanMemberInfo(
    long ClanId,
    long UserId,
    string DisplayName,
    string Role,
    DateTimeOffset JoinedAt);
