using BotFramework.Sdk;

namespace Games.Poker;

public sealed record PokerPlayerJoined(string InviteCode, long UserId, int Position, int BuyIn, long OccurredAt) : IDomainEvent
{
    public string EventType => "poker.player_joined";
}
