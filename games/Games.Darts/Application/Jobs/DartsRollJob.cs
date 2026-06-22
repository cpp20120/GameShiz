namespace Games.Darts.Application.Jobs;

public readonly record struct DartsRollJob(
    long RoundId,
    long ChatId,
    long UserId,
    string DisplayName,
    int ReplyToMessageId);
