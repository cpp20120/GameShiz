using BotFramework.Sdk;

namespace Games.SecretHitler;

public sealed record SecretHitlerGameStarted(string InviteCode, int Players, long OccurredAt) : IDomainEvent
{
    public string EventType => "sh.game_started";
}
