namespace Games.Bowling.Application.Execution;

public sealed record BowlingPlaceBetCommand(
    long UserId, string DisplayName, long ChatId, int Amount, string CommandId, int MaxBet, string? BlockingGameId);

public sealed record BowlingRollCommand(
    long UserId, string DisplayName, long ChatId, int Face, string CommandId, double RedeemDropChance);

public sealed record BowlingAbortCommand(long UserId, string DisplayName, long ChatId, string CommandId);

public sealed record BowlingAbortResult(bool Aborted);
