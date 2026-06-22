namespace Games.Meta.Domain.Clans;

public sealed record ClanJoinResult(bool Joined, string Message, ClanInfo? Clan = null);
