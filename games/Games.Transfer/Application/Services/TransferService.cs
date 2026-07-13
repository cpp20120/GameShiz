
using System.Globalization;
using BotFramework.Host.Execution;
using Games.Transfer.Application.Execution;

namespace Games.Transfer.Application.Services;

public sealed class TransferService(
    IAtomicGameExecutor<TransferCommand, TransferState, TransferAttemptResult> executor,
    IRuntimeTuningAccessor tuning) : ITransferService
{
    public Task<TransferAttemptResult> TryTransferAsync(
        long fromUserId,
        long toUserId,
        long chatId,
        string senderDisplayName,
        string recipientDisplayName,
        int netToRecipient,
        CancellationToken ct) =>
        TryTransferAsync(fromUserId, toUserId, chatId, senderDisplayName, recipientDisplayName, netToRecipient, 0, ct);

    public async Task<TransferAttemptResult> TryTransferAsync(
        long fromUserId,
        long toUserId,
        long chatId,
        string senderDisplayName,
        string recipientDisplayName,
        int netToRecipient,
        int sourceMessageId,
        CancellationToken ct)
    {
        var opts = tuning.GetSection<TransferOptions>(TransferOptions.SectionName);
        if (fromUserId == toUserId)
            return Fail(TransferError.SameUser, netToRecipient);

        if (netToRecipient < opts.MinNetCoins)
            return Fail(TransferError.NetBelowMinimum, netToRecipient);

        if (opts.MaxNetCoins > 0 && netToRecipient > opts.MaxNetCoins)
            return Fail(TransferError.NetAboveMaximum, netToRecipient);

        var fee = TransferOptions.ComputeFeeCoins(netToRecipient, opts.FeePercent, opts.MinFeeCoins);
        var total = netToRecipient + fee;

        var commandId = string.Create(CultureInfo.InvariantCulture,
            $"peer:{chatId}:{sourceMessageId}:{fromUserId}:{toUserId}");
        return await executor.ExecuteAsync(new(new TransferCommand(
            fromUserId, toUserId, chatId, senderDisplayName, recipientDisplayName,
            netToRecipient, fee, total, commandId)), ct).ConfigureAwait(false);
    }

    private static TransferAttemptResult Fail(TransferError error, int net) =>
        new(error, net, 0, 0, 0, 0);
}
