
namespace Games.Basketball.Domain.Events;

public sealed record BasketballBetPlaced(
    long UserId,
    long ChatId,
    int Amount,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "basketball.bet_placed";
}

public sealed record BasketballThrowCompleted(
    long UserId,
    long ChatId,
    int Face,
    int Bet,
    int Multiplier,
    int Payout,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "basketball.throw_completed";
}

public sealed record BasketballBetAborted(
    long UserId,
    long ChatId,
    int Amount,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "basketball.bet_aborted";
}
