namespace Games.Dice.Contracts.Play;

public sealed record DicePlayResponse(
    DicePlayStatus Status,
    int Prize,
    int Stake,
    int Balance,
    int Tax,
    int DailyRollsUsed,
    int DailyRollLimit);
