using BotFramework.Host;

namespace Games.Transfer.Application.Services;

public interface ITransferService
{
    Task<TransferAttemptResult> TryTransferAsync(
        long fromUserId,
        long toUserId,
        long chatId,
        string senderDisplayName,
        string recipientDisplayName,
        int netToRecipient,
        CancellationToken ct);

    Task<TransferAttemptResult> TryTransferAsync(
        long fromUserId,
        long toUserId,
        long chatId,
        string senderDisplayName,
        string recipientDisplayName,
        int netToRecipient,
        int sourceMessageId,
        CancellationToken ct);
}
