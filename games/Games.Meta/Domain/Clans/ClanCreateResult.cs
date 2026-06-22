namespace Games.Meta;

public sealed record ClanCreateResult(bool Created, string Message, ClanInfo? Clan = null);
