
namespace Games.Darts.Domain.Events;

public sealed record DartsBetAborted(
    long UserId,
    long ChatId,
    int Amount,
    long RoundId,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "darts.bet_aborted";
}
