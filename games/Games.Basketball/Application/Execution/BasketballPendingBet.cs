namespace Games.Basketball.Application.Execution;

public sealed record BasketballPendingBet(
    long UserId,
    long ChatId,
    int Amount,
    DateTimeOffset CreatedAt);
