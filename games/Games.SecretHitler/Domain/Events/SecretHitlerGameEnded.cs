
namespace Games.SecretHitler.Domain.Events;

public sealed record SecretHitlerGameEnded(
    string InviteCode,
    ShWinner Winner,
    ShWinReason Reason,
    IReadOnlyList<SecretHitlerPayout> Payouts,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "sh.game_ended";
}
