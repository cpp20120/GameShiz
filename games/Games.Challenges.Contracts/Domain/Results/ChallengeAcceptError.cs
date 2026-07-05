namespace Games.Challenges.Domain.Results;

public enum ChallengeAcceptError
{
    None,
    NotFound,
    NotTarget,
    Expired,
    AlreadyResolved,
    ChallengerNotEnoughCoins,
    TargetNotEnoughCoins,
    RollFailed,
}
