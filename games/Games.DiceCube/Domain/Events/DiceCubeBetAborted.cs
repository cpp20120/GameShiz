
namespace Games.DiceCube.Domain.Events;

public sealed record DiceCubeBetAborted(
    long UserId,
    long ChatId,
    int Amount,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "dicecube.bet_aborted";
}
