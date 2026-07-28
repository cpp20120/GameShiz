namespace Games.Football.Application.Execution;

public sealed record FootballAbortCommand(long UserId, string DisplayName, long ChatId, string CommandId);
