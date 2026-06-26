
namespace Games.SecretHitler.Domain.Events;

public sealed record SecretHitlerPlayerJoined(string InviteCode, long UserId, int Position, int BuyIn, long OccurredAt) : IDomainEvent
{
    public string EventType => "sh.player_joined";
}
