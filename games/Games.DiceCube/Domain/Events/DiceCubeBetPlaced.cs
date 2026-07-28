
namespace Games.DiceCube.Domain.Events;

public sealed record DiceCubeBetPlaced(
    long UserId,
    long ChatId,
    int Amount,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "dicecube.bet_placed";
}
