namespace BotFramework.Host;

public readonly record struct DailyBonusClaimResult(
    DailyBonusClaimStatus Status,
    int BonusCoins = 0,
    int NewBalance = 0);
