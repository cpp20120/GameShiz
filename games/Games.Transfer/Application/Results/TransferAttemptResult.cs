using BotFramework.Host;

namespace Games.Transfer;

public readonly record struct TransferAttemptResult(
    TransferError Error,
    int NetToRecipient,
    int FeeCoins,
    int TotalDebited,
    int SenderBalance,
    int RecipientBalance);
