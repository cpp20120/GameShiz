namespace Games.Darts.Application.Execution;

public sealed record DartsQuickThrowCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    int DiceMessageId,
    int Face,
    int Amount,
    int MaxBet,
    double RedeemDropChance,
    string? BlockingGameId);
