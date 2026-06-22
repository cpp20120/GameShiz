namespace Games.Challenges;

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
