namespace Games.Blackjack.Application.Execution;

public sealed record BlackjackHandState(
    string HandId,
    long UserId,
    long ChatId,
    int Bet,
    string[] PlayerCards,
    string[] DealerCards,
    string DeckState,
    int? StateMessageId,
    DateTimeOffset CreatedAt);
