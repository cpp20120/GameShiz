namespace Games.Pick.Domain.Events;

public sealed record PickPlayed(
    long UserId,
    long ChatId,
    int Bet,
    int Variants,
    int Backed,
    int PickedIndex,
    bool Won,
    int Payout,
    int StreakAfter,
    int ChainDepth,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "pick.played";
}

public sealed record PickLotteryOpened(
    Guid LotteryId,
    long UserId,
    long ChatId,
    int Stake,
    long OccurredAt) : IDomainEvent
{
    public string EventType => "pick.lottery_opened";
}

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
