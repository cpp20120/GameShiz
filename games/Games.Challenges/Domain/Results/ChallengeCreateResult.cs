namespace Games.Challenges.Domain.Results;

public sealed record ChallengeCreateResult(
    ChallengeCreateError Error,
    Challenge? Challenge = null,
    int Balance = 0);
