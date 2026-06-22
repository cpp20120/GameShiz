using BotFramework.Sdk;

namespace Games.Blackjack.Domain.Events;

public sealed record BlackjackHandClosed(
    long UserId,
    long ChatId,
    long CreatedAtMs,
    string Reason,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "blackjack.hand_closed";
}
