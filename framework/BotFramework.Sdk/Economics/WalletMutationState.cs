using System.Runtime.InteropServices;

namespace BotFramework.Sdk.Economics;

[StructLayout(LayoutKind.Auto)]
public readonly record struct WalletMutationState(int Balance, long Version);
