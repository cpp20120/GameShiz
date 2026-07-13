
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

public sealed record DartsThrowCompleted(
    long UserId,
    long ChatId,
    int Face,
    int Bet,
    int Multiplier,
    int Payout,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "darts.throw_completed";
}

public sealed record DartsBetAborted(
    long UserId,
    long ChatId,
    int Amount,
    long RoundId,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "darts.bet_aborted";
}
