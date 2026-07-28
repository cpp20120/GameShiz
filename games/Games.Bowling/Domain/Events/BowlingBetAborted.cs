
namespace Games.Bowling.Domain.Events;

public sealed record BowlingBetAborted(long UserId, long ChatId, int Amount, long OccurredAt) : IDomainEvent
{
    public string EventType => "bowling.bet_aborted";
}
