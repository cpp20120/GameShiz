namespace Games.Challenges.Domain.Results;

public enum ChallengeCreateError
{
    None,
    Usage,
    InvalidGame,
    InvalidAmount,
    TargetNotFound,
    SelfChallenge,
    NotEnoughCoins,
    AlreadyPending,
}
