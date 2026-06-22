namespace BotFramework.Host;

public readonly record struct DailyBonusCatchUpStats(
    int Wallets,
    int Days,
    int CreditedCoins,
    int SkippedDays);
