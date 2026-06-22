namespace Games.Football;

public sealed record FootballThrowResult(
    FootballThrowOutcome Outcome,
    int Face = 0,
    int Bet = 0,
    int Multiplier = 0,
    int Payout = 0,
    int Balance = 0,
    int DailyRollUsed = 0,
    int DailyRollLimit = 0);
