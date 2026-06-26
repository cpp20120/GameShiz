
namespace Games.Blackjack.Domain.Models;

public sealed record BlackjackStateMessageSet(
    long UserId,
    int StateMessageId,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "blackjack.state_message_set";
}
