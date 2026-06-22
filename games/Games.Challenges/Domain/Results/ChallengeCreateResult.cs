namespace Games.Challenges;

public sealed record ChallengeCreateResult(
    ChallengeCreateError Error,
    Challenge? Challenge = null,
    int Balance = 0);
