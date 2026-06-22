namespace Games.Meta;

public sealed record TournamentMatchInfo(
    long Id,
    long TournamentId,
    int Round,
    int MatchIndex,
    string Status,
    long? Player1UserId,
    string? Player1DisplayName,
    long? Player2UserId,
    string? Player2DisplayName,
    long? VictorUserId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
