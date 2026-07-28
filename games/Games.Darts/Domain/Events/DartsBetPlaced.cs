
namespace Games.Darts.Domain.Events;

public sealed record DartsBetPlaced(
    long UserId,
    long ChatId,
    int Amount,
    long RoundId,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "darts.bet_placed";
}
