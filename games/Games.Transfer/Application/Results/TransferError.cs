
namespace Games.Transfer.Application.Results;

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
