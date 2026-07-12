namespace Games.Blackjack.Application.Execution;

public sealed record BlackjackTimeoutCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    string HandId,
    string CommandId);
