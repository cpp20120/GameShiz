namespace Games.Basketball.Application.Execution;

public sealed record BasketballThrowCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    int Face,
    string CommandId,
    double RedeemDropChance);
