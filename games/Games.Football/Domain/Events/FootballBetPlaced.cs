
namespace Games.Football.Domain.Events;

public sealed record FootballBetPlaced(long UserId, long ChatId, int Amount, long OccurredAt) : IDomainEvent
{
    public string EventType => "football.bet_placed";
}
