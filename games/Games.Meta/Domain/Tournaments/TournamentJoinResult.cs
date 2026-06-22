namespace Games.Meta.Domain.Tournaments;

public sealed record TournamentJoinResult(bool Joined, string Message, TournamentInfo? Tournament = null);
