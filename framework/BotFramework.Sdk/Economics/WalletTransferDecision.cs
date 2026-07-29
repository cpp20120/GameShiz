using System.Runtime.InteropServices;
using BotFramework.Host.Contracts.Economics;

namespace BotFramework.Sdk.Economics;

[StructLayout(LayoutKind.Auto)]
public readonly record struct WalletTransferDecision(
    bool Applied,
    PeerTransferFailure? Failure,
    int SenderNewBalance,
    int RecipientNewBalance);
