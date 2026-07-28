namespace Games.Football.Application.Execution;

public sealed record FootballThrowCommand(
    long UserId, string DisplayName, long ChatId, int Face, string CommandId, double RedeemDropChance);
