using System.Runtime.InteropServices;

namespace BotFramework.Host.Contracts.Economics;

[StructLayout(LayoutKind.Auto)]
public readonly record struct LedgerRevertResult(LedgerRevertStatus Status, int NewBalance = 0);
