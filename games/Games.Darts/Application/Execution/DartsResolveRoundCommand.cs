namespace Games.Darts.Application.Execution;

public sealed record DartsResolveRoundCommand(
    long RoundId,
    long UserId,
    string DisplayName,
    long ChatId,
    int BotDiceMessageId,
    int Face,
    string CommandId,
    double RedeemDropChance);
