namespace Games.Dice.Application.Execution;

public sealed record DiceCommand(
    long UserId,
    string DisplayName,
    int DiceValue,
    long ChatId,
    int SourceMessageId,
    bool IsForwarded,
    int Cost,
    double RedeemDropChance);
