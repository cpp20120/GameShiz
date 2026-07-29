using BotFramework.Host.Contracts.Economics;

namespace BotFramework.Sdk.Economics;

public static class WalletTransferPolicy
{
    public static WalletTransferDecision Apply(
        int senderBalance,
        int recipientBalance,
        int debitFromSender,
        int creditToRecipient)
    {
        if (debitFromSender <= 0 || creditToRecipient <= 0)
            throw new ArgumentOutOfRangeException(nameof(debitFromSender));
        if (debitFromSender < creditToRecipient)
            throw new ArgumentException("Debit must be >= credit (fee cannot be negative).", nameof(debitFromSender));
        if (senderBalance < debitFromSender)
            return new(false, PeerTransferFailure.InsufficientFunds, 0, 0);

        return new(
            true,
            null,
            checked(senderBalance - debitFromSender),
            checked(recipientBalance + creditToRecipient));
    }
}
