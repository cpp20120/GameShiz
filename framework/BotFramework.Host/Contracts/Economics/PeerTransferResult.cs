namespace BotFramework.Host;

public readonly record struct PeerTransferResult(
    bool Ok,
    PeerTransferFailure? Failure,
    int SenderNewBalance,
    int RecipientNewBalance);
