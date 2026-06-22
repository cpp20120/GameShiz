namespace Games.Meta;

public sealed record TournamentCreateResult(bool Created, string Message, TournamentInfo? Tournament = null);
