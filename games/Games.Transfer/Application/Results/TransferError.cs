using BotFramework.Host;

namespace Games.Transfer;

public enum TransferError
{
    None,
    NetBelowMinimum,
    NetAboveMaximum,
    SameUser,
    InsufficientFunds,
    SenderMissing,
    RecipientMissing,
}
