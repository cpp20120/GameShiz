namespace Games.Bowling.Domain.Results;

public sealed record BowlingRollResult(
    BowlingRollOutcome Outcome,
    int Face = 0,
    int Bet = 0,
    int Multiplier = 0,
    int Payout = 0,
    int Balance = 0,
    int DailyRollUsed = 0,
    int DailyRollLimit = 0);
