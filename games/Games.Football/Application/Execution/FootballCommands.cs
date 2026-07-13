namespace Games.Football.Application.Execution;

public sealed record FootballPlaceBetCommand(
    long UserId, string DisplayName, long ChatId, int Amount, string CommandId, int MaxBet, string? BlockingGameId);

public sealed record FootballThrowCommand(
    long UserId, string DisplayName, long ChatId, int Face, string CommandId, double RedeemDropChance);

public sealed record FootballAbortCommand(long UserId, string DisplayName, long ChatId, string CommandId);

public sealed record FootballAbortResult(bool Aborted);
