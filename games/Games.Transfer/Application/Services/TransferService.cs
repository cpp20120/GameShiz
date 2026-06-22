using BotFramework.Host;

namespace Games.Transfer.Application.Services;

public sealed class TransferService(
    IEconomicsService economics,
    IAnalyticsService analytics,
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

        await economics.EnsureUserAsync(fromUserId, chatId, senderDisplayName, ct);
        await economics.EnsureUserAsync(toUserId, chatId, recipientDisplayName, ct);

        var operationId = $"peer:{chatId}:{sourceMessageId}:{fromUserId}:{toUserId}";
        var result = await economics.TryPeerTransferOnceAsync(
            fromUserId,
            toUserId,
            chatId,
            total,
            netToRecipient,
            "transfer.send",
            "transfer.receive",
            operationId,
            ct);

        if (!result.Ok)
        {
            var err = result.Failure switch
            {
                PeerTransferFailure.SameUser => TransferError.SameUser,
                PeerTransferFailure.SenderMissing => TransferError.SenderMissing,
                PeerTransferFailure.RecipientMissing => TransferError.RecipientMissing,
                PeerTransferFailure.InsufficientFunds => TransferError.InsufficientFunds,
                _ => TransferError.SenderMissing,
            };
            var bal = await economics.GetBalanceAsync(fromUserId, chatId, ct);
            return new TransferAttemptResult(err, netToRecipient, fee, total, bal, 0);
        }

        analytics.Track("transfer", "completed", new Dictionary<string, object?>
        {
            ["from_user_id"] = fromUserId,
            ["to_user_id"] = toUserId,
            ["chat_id"] = chatId,
            ["net"] = netToRecipient,
            ["fee"] = fee,
            ["total"] = total,
        });

        return new TransferAttemptResult(
            TransferError.None,
            netToRecipient,
            fee,
            total,
            result.SenderNewBalance,
            result.RecipientNewBalance);
    }

    private static TransferAttemptResult Fail(TransferError error, int net) =>
        new(error, net, 0, 0, 0, 0);
}