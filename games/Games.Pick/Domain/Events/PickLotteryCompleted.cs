namespace Games.Pick.Domain.Events;

public sealed record PickLotteryCompleted(
    Guid LotteryId,
    long ChatId,
    bool Cancelled,
    long? WinnerId,
    int Entrants,
    int Pot,
    int Payout,
    int Fee,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "pick.lottery_completed";
}
