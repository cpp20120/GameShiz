namespace Games.Bowling.Application.Execution;

public sealed record BowlingPlaceBetCommand(
    long UserId, string DisplayName, long ChatId, int Amount, string CommandId, int MaxBet, string? BlockingGameId);
