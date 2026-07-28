namespace Games.Pick.Domain.Events;

public sealed record PickDailyTicketsBought(
    Guid LotteryId,
    long UserId,
    long ChatId,
    int Count,
    int Cost,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "pick.daily_tickets_bought";
}
