namespace Games.Bowling.Application.Execution;

public sealed record BowlingRollCommand(
    long UserId, string DisplayName, long ChatId, int Face, string CommandId, double RedeemDropChance);
