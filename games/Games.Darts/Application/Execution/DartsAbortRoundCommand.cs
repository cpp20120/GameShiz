namespace Games.Darts.Application.Execution;

public sealed record DartsAbortRoundCommand(
    long RoundId,
    long UserId,
    string DisplayName,
    long ChatId,
    string CommandId);
