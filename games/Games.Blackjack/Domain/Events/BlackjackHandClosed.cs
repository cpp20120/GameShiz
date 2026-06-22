using BotFramework.Sdk;

namespace Games.Blackjack;

public sealed record BlackjackHandClosed(
    long UserId,
    long ChatId,
    long CreatedAtMs,
    string Reason,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "blackjack.hand_closed";
}
