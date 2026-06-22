namespace Games.Meta.Domain.Tournaments;

public sealed record TournamentInfo(
    long Id,
    long SeasonId,
    long ChatId,
    string GameKey,
    string Type,
    string Status,
    int EntryFee,
    int MaxPlayers,
    long CreatedBy,
    DateTimeOffset CreatedAt,
    int PlayerCount,
    long PrizePool);
