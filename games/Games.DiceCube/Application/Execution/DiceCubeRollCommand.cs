namespace Games.DiceCube.Application.Execution;

public sealed record DiceCubeRollCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    int Face,
    string CommandId,
    double RedeemDropChance);
