using BotFramework.Sdk;

namespace Games.SecretHitler;

public sealed record SecretHitlerPlayerJoined(string InviteCode, long UserId, int Position, int BuyIn, long OccurredAt) : IDomainEvent
{
    public string EventType => "sh.player_joined";
}
