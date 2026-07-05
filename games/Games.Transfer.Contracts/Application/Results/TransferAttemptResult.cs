
using System.Runtime.InteropServices;

namespace Games.Transfer.Application.Results;

[StructLayout(LayoutKind.Auto)]
public readonly record struct TransferAttemptResult(
    TransferError Error,
    int NetToRecipient,
    int FeeCoins,
    int TotalDebited,
    int SenderBalance,
    int RecipientBalance);
