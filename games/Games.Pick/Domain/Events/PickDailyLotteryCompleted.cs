namespace Games.Pick.Domain.Events;

public sealed record PickDailyLotteryCompleted(
    Guid LotteryId,
    long ChatId,
    bool Cancelled,
    long? WinnerId,
    int Tickets,
    int Pot,
    int Payout,
    int Fee,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "pick.daily_lottery_completed";
}
