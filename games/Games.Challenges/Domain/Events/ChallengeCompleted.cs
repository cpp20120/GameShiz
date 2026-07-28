namespace Games.Challenges.Domain.Events;

public sealed record ChallengeCompleted(Guid ChallengeId, long ChatId, int ChallengerRoll, int TargetRoll,
    long WinnerId, int Payout, int Fee, bool IsTie, long OccurredAt) : IDomainEvent
{
    public string EventType => "challenge.completed";
}
