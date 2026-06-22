using BotFramework.Sdk;

namespace Games.Poker;

public sealed record PokerHandStarted(string InviteCode, int Players, long OccurredAt) : IDomainEvent
{
    public string EventType => "poker.hand_started";
}
