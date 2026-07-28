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
