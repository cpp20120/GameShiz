using BotFramework.Sdk;

namespace Games.SecretHitler;

public sealed record SecretHitlerGameCreated(string InviteCode, long HostUserId, int BuyIn, long OccurredAt) : IDomainEvent
{
    public string EventType => "sh.game_created";
}
