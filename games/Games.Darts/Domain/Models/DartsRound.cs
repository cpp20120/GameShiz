namespace Games.Darts;

public sealed record DartsRound(
    long Id,
    long UserId,
    long ChatId,
    int Amount,
    DateTimeOffset CreatedAt,
    DartsRoundStatus Status,
    int? BotMessageId,
    int ReplyToMessageId);
