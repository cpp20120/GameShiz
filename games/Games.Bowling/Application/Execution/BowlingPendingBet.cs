namespace Games.Bowling.Application.Execution;

public sealed record BowlingPendingBet(long UserId, long ChatId, int Amount, DateTimeOffset CreatedAt);
