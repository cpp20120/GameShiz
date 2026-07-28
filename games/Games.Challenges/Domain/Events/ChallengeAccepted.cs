namespace Games.Challenges.Domain.Events;

public sealed record ChallengeAccepted(Guid ChallengeId, long ChatId, long ChallengerId, long TargetId,
    int Amount, long OccurredAt) : IDomainEvent
{
    public string EventType => "challenge.accepted";
}
