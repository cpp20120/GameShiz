namespace Games.Challenges.Domain.Events;

public sealed record ChallengeCreated(Guid ChallengeId, long ChatId, long ChallengerId, long TargetId,
    int Amount, string Game, long OccurredAt) : IDomainEvent
{
    public string EventType => "challenge.created";
}
