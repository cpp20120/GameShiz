namespace Games.Pick.Domain.Events;

public sealed record PickLotteryJoined(
    Guid LotteryId,
    long UserId,
    long ChatId,
    int Stake,
    int Entrants,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "pick.lottery_joined";
}
