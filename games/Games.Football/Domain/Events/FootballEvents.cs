
namespace Games.Football.Domain.Events;

public sealed record FootballBetPlaced(long UserId, long ChatId, int Amount, long OccurredAt) : IDomainEvent
{
    public string EventType => "football.bet_placed";
}

public sealed record FootballThrowCompleted(
    long UserId,
    long ChatId,
    int Face,
    int Bet,
    int Multiplier,
    int Payout,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "football.throw_completed";
}

public sealed record FootballBetAborted(long UserId, long ChatId, int Amount, long OccurredAt) : IDomainEvent
{
    public string EventType => "football.bet_aborted";
}
