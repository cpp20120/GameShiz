using BotFramework.Sdk;

namespace Games.Poker;

public sealed record PokerTableCreated(string InviteCode, long HostUserId, int BuyIn, long OccurredAt) : IDomainEvent
{
    public string EventType => "poker.table_created";
}
