namespace Games.Bowling.Application.Execution;

public sealed record BowlingAbortCommand(long UserId, string DisplayName, long ChatId, string CommandId);
