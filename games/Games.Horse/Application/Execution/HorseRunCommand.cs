namespace Games.Horse.Application.Execution;

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
