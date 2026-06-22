namespace Games.Meta;

public sealed record TournamentJoinResult(bool Joined, string Message, TournamentInfo? Tournament = null);
