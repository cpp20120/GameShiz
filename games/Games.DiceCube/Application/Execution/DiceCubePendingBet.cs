namespace Games.DiceCube.Application.Execution;

public sealed record DiceCubePendingBet(
    long UserId,
    long ChatId,
    int Amount,
    DateTimeOffset CreatedAt,
    int Mult4,
    int Mult5,
    int Mult6);
