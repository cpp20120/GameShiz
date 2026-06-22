using BotFramework.Sdk;

namespace Games.Poker.Domain.Events;

public sealed record PokerHandStarted(string InviteCode, int Players, long OccurredAt) : IDomainEvent
{
    public string EventType => "poker.hand_started";
}
