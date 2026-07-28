namespace Games.Football.Application.Execution;

public sealed record FootballPlaceBetCommand(
    long UserId, string DisplayName, long ChatId, int Amount, string CommandId, int MaxBet, string? BlockingGameId);
