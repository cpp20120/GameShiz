namespace BotFramework.Host.Contracts.Economics;

public enum DailyBonusClaimStatus
{
    Claimed,
    AlreadyClaimedToday,
    Disabled,
    IneligibleEmptyBalance,
    IneligiblePercentRoundsToZero,
}
