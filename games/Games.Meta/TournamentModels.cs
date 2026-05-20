namespace Games.Meta;

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

public sealed record TournamentPlayerInfo(
    long TournamentId,
    long UserId,
    string DisplayName,
    string Status,
    DateTimeOffset JoinedAt);

public sealed record TournamentCreateResult(bool Created, string Message, TournamentInfo? Tournament = null);
public sealed record TournamentJoinResult(bool Joined, string Message, TournamentInfo? Tournament = null);
