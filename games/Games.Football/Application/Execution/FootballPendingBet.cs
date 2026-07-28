namespace Games.Football.Application.Execution;

public sealed record FootballPendingBet(long UserId, long ChatId, int Amount, DateTimeOffset CreatedAt);
