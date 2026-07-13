
namespace Games.DiceCube.Domain.Events;

public sealed record DiceCubeBetPlaced(
    long UserId,
    long ChatId,
    int Amount,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "dicecube.bet_placed";
}

public sealed record DiceCubeBetAborted(
    long UserId,
    long ChatId,
    int Amount,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "dicecube.bet_aborted";
}

public sealed record DiceCubeRollCompleted(
    long UserId,
    long ChatId,
    int Face,
    int Bet,
    int Multiplier,
    int Payout,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "dicecube.roll_completed";
}
