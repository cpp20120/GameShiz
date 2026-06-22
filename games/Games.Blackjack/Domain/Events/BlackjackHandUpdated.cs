using BotFramework.Sdk;

namespace Games.Blackjack.Domain.Events;

public sealed record BlackjackHandUpdated(
    long UserId,
    long ChatId,
    int Bet,
    string PlayerCards,
    string DealerCards,
    string DeckState,
    int? StateMessageId,
    long CreatedAtMs,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "blackjack.hand_updated";
}
