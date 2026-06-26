using Dapper;

namespace Games.Blackjack.Infrastructure.Persistence;

public sealed record BlackjackHandRow(
    long UserId,
    long ChatId,
    int Bet,
    string PlayerCards,
    string DealerCards,
    string DeckState,
    int? StateMessageId,
    DateTimeOffset CreatedAt);
