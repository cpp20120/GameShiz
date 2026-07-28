
namespace Games.Bowling.Domain.Events;

public sealed record BowlingBetPlaced(long UserId, long ChatId, int Amount, long OccurredAt) : IDomainEvent
{
    public string EventType => "bowling.bet_placed";
}
