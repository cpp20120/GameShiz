using System.Runtime.InteropServices;

namespace BotFramework.Host.Contracts.Economics;

[StructLayout(LayoutKind.Auto)]
public readonly record struct DailyBonusClaimResult(
    DailyBonusClaimStatus Status,
    int BonusCoins = 0,
    int NewBalance = 0);
