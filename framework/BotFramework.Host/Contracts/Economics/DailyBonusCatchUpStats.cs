using System.Runtime.InteropServices;

namespace BotFramework.Host.Contracts.Economics;

[StructLayout(LayoutKind.Auto)]
public readonly record struct DailyBonusCatchUpStats(
    int Wallets,
    int Days,
    int CreditedCoins,
    int SkippedDays);
