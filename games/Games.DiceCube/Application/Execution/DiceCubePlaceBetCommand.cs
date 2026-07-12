namespace Games.DiceCube.Application.Execution;

public sealed record DiceCubePlaceBetCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    int Amount,
    string CommandId,
    int MaxBet,
    int Mult4,
    int Mult5,
    int Mult6,
    int CooldownSeconds,
    string? BlockingGameId);
