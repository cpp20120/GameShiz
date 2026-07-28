namespace Games.Darts.Application.Execution;

public sealed record DartsPlaceBetCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    int Amount,
    int ReplyToMessageId,
    long RoundId,
    string CommandId,
    int MaxBet,
    string? BlockingGameId);
