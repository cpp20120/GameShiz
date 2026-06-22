namespace Games.Challenges;

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
