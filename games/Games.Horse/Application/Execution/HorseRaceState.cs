namespace Games.Horse.Application.Execution;

public sealed record HorseRaceState(
    IReadOnlyList<HorseBetRow> Bets,
    IReadOnlyList<long> ResultScopes,
    int? Winner);
