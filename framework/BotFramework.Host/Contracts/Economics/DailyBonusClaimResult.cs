namespace BotFramework.Host.Contracts.Economics;

public readonly record struct DailyBonusClaimResult(
    DailyBonusClaimStatus Status,
    int BonusCoins = 0,
    int NewBalance = 0);
