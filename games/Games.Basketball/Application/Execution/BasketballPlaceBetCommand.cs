namespace Games.Basketball.Application.Execution;

public sealed record BasketballPlaceBetCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    int Amount,
    string CommandId,
    int MaxBet,
    string? BlockingGameId);
