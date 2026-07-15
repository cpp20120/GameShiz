using System.Runtime.InteropServices;

namespace BotFramework.Host.Contracts.Economics;

[StructLayout(LayoutKind.Auto)]
public readonly record struct TelegramDiceRollGateResult(
    TelegramDiceRollGateStatus Status,
    int UsedToday = 0,
    int Limit = 0);
