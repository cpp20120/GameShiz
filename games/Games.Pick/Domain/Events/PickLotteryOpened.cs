namespace Games.Pick.Domain.Events;

public sealed record PickLotteryOpened(
    Guid LotteryId,
    long UserId,
    long ChatId,
    int Stake,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "pick.lottery_opened";
}
