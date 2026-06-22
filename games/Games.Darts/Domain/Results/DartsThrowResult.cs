namespace Games.Darts;

public sealed record DartsThrowResult(
    DartsThrowOutcome Outcome,
    int Face = 0,
    int Bet = 0,
    int Multiplier = 0,
    int Payout = 0,
    int Balance = 0,
    // Quick-play error context (only set when Outcome is a Bet* error)
    string? BlockingGameId = null,
    int DailyRollUsed = 0,
    int DailyRollLimit = 0);
