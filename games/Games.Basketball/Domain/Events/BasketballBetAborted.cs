
namespace Games.Basketball.Domain.Events;

public sealed record BasketballBetAborted(
    long UserId,
    long ChatId,
    int Amount,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "basketball.bet_aborted";
}
