namespace Games.Meta.Domain.Tournaments;

public sealed record TournamentCreateResult(bool Created, string Message, TournamentInfo? Tournament = null);
