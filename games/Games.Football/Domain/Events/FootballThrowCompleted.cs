
namespace Games.Football.Domain.Events;

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
