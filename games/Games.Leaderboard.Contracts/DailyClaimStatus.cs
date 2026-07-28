namespace Games.Leaderboard.Contracts;

public enum DailyClaimStatus
{
    Claimed,
    AlreadyClaimedToday,
    Disabled,
    IneligibleEmptyBalance,
    IneligiblePercentRoundsToZero,
}
