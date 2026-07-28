namespace Games.Challenges.Domain.Events;

public sealed record ChallengeStatusChanged(Guid ChallengeId, long ChatId, string Status, long OccurredAt) : IDomainEvent
{
    public string EventType => "challenge.status_changed";
}
