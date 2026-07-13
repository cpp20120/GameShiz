namespace Games.Challenges.Domain.Entities;

public sealed record Challenge(
    Guid Id,
    long ChatId,
    long ChallengerId,
    string ChallengerName,
    long TargetId,
    string TargetName,
    int Amount,
    ChallengeGame Game,
    ChallengeStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt);
