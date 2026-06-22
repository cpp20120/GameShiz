namespace Games.Meta.Domain.Clans;

public sealed record ClanCreateResult(bool Created, string Message, ClanInfo? Clan = null);
