namespace Games.Darts.Application.Execution;

public sealed record DartsQueuedState(DartsRound? Round, int QueuedAhead);

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

public sealed record DartsResolveRoundCommand(
    long RoundId,
    long UserId,
    string DisplayName,
    long ChatId,
    int BotDiceMessageId,
    int Face,
    string CommandId,
    double RedeemDropChance);

public sealed record DartsAbortRoundCommand(
    long RoundId,
    long UserId,
    string DisplayName,
    long ChatId,
    string CommandId);

public sealed record DartsAbortRoundResult(bool Aborted);
