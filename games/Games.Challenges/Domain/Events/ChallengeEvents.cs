namespace Games.Challenges.Domain.Events;

public sealed record ChallengeCreated(Guid ChallengeId, long ChatId, long ChallengerId, long TargetId,
    int Amount, string Game, long OccurredAt) : IDomainEvent
{
    public string EventType => "challenge.created";
}

public sealed record ChallengeAccepted(Guid ChallengeId, long ChatId, long ChallengerId, long TargetId,
    int Amount, long OccurredAt) : IDomainEvent
{
    public string EventType => "challenge.accepted";
}

public sealed record ChallengeCompleted(Guid ChallengeId, long ChatId, int ChallengerRoll, int TargetRoll,
    long WinnerId, int Payout, int Fee, bool IsTie, long OccurredAt) : IDomainEvent
{
    public string EventType => "challenge.completed";
}

public sealed record ChallengeStatusChanged(Guid ChallengeId, long ChatId, string Status, long OccurredAt) : IDomainEvent
{
    public string EventType => "challenge.status_changed";
}
