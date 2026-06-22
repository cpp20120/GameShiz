namespace Games.Meta;

public sealed record TournamentPlayerInfo(
    long TournamentId,
    long UserId,
    string DisplayName,
    string Status,
    DateTimeOffset JoinedAt);
