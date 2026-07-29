using System.Runtime.InteropServices;

namespace BotFramework.Sdk.Economics;

[StructLayout(LayoutKind.Auto)]
public readonly record struct WalletMutationLine(int Delta, int BalanceAfter, string Reason);
