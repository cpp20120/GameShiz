using BotFramework.Sdk;

namespace Games.Blackjack;

public sealed record BlackjackStateMessageSet(
    long UserId,
    int StateMessageId,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "blackjack.state_message_set";
}
