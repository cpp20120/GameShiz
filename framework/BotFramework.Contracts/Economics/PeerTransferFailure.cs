namespace BotFramework.Host.Contracts.Economics;

public enum PeerTransferFailure
{
    SameUser,
    SenderMissing,
    RecipientMissing,
    InsufficientFunds,
}
