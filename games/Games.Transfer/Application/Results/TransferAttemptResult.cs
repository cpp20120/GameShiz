using BotFramework.Host;

namespace Games.Transfer.Application.Results;

public readonly record struct TransferAttemptResult(
    TransferError Error,
    int NetToRecipient,
    int FeeCoins,
    int TotalDebited,
    int SenderBalance,
    int RecipientBalance);
