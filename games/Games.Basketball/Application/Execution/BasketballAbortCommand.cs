namespace Games.Basketball.Application.Execution;

public sealed record BasketballAbortCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    string CommandId);
