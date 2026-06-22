namespace Games.Meta;

public sealed record ClanJoinResult(bool Joined, string Message, ClanInfo? Clan = null);
