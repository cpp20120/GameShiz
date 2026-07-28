
namespace Games.Basketball.Domain.Events;

public sealed record BasketballBetPlaced(
    long UserId,
    long ChatId,
    int Amount,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "basketball.bet_placed";
}
