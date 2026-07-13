
namespace Games.Bowling.Domain.Events;

public sealed record BowlingBetPlaced(long UserId, long ChatId, int Amount, long OccurredAt) : IDomainEvent
{
    public string EventType => "bowling.bet_placed";
}

public sealed record BowlingRollCompleted(
    long UserId,
    long ChatId,
    int Face,
    int Bet,
    int Multiplier,
    int Payout,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "bowling.roll_completed";
}

public sealed record BowlingBetAborted(long UserId, long ChatId, int Amount, long OccurredAt) : IDomainEvent
{
    public string EventType => "bowling.bet_aborted";
}
