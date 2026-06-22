namespace BotFramework.Host.Contracts.Economics;

public readonly record struct DailyBonusCatchUpStats(
    int Wallets,
    int Days,
    int CreditedCoins,
    int SkippedDays);
