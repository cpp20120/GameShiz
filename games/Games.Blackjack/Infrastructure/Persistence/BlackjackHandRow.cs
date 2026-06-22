using BotFramework.Host;
using BotFramework.Sdk;
using Dapper;

namespace Games.Blackjack;

public sealed record BlackjackHandRow(
    long UserId,
    long ChatId,
    int Bet,
    string PlayerCards,
    string DealerCards,
    string DeckState,
    int? StateMessageId,
    DateTimeOffset CreatedAt);
