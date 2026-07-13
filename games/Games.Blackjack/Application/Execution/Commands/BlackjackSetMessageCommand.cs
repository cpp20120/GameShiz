namespace Games.Blackjack.Application.Execution;

public sealed record BlackjackSetMessageCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    string HandId,
    int MessageId,
    string CommandId);
