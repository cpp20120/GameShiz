namespace Games.Challenges.Application.Execution;

public sealed record ChallengeExecutionState(
    Challenge? Challenge,
    bool HasPendingPair,
    int ChallengerBalance,
    int TargetBalance);
