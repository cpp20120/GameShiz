namespace Games.Blackjack.Application.Execution;

public sealed record BlackjackStartCommand(
    long UserId,
    string DisplayName,
    long ChatId,
    int Bet,
    string CommandId,
    int MinBet,
    int MaxBet,
    int HandTimeoutMs);
