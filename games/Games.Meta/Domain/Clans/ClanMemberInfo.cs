namespace Games.Meta;

public sealed record ClanMemberInfo(
    long ClanId,
    long UserId,
    string DisplayName,
    string Role,
    DateTimeOffset JoinedAt);
