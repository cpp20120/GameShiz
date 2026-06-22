namespace BotFramework.Host;

public enum DailyBonusClaimStatus
{
    Claimed,
    AlreadyClaimedToday,
    Disabled,
    IneligibleEmptyBalance,
    IneligiblePercentRoundsToZero,
}
