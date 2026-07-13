namespace Games.Horse.Application.Execution;

public sealed record HorseBetState(HorseBetRow? Bet);

public sealed record HorsePlaceBetCommand(
    long UserId,
    string DisplayName,
    long BalanceScopeId,
    int HorseId,
    int Amount,
    string RaceDate,
    Guid BetId,
    string CommandId,
    int HorseCount);

public sealed record HorseRaceState(
    IReadOnlyList<HorseBetRow> Bets,
    IReadOnlyList<long> ResultScopes,
    int? Winner);

public sealed record HorseRunCommand(
    long CallerUserId,
    HorseRunKind Kind,
    long ChatScopeId,
    long ResultScopeId,
    string RaceDate,
    IReadOnlyList<HorseBetRow> ExpectedBets,
    string CommandId,
    int HorseCount,
    int MinBetsToRun,
    bool IsAdmin);
