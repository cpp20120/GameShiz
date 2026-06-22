namespace BotFramework.Host;

public interface IEconomicsService
{
    /// <summary>
    /// Creates the wallet row if it does not exist yet and updates the display name on existing rows.
    /// </summary>
    /// <param name="userId">Telegram user id.</param>
    /// <param name="balanceScopeId">Telegram chat id for this wallet. In private chats equals the user's id.</param>
    /// <param name="displayName">Latest Telegram display name to store with the wallet.</param>
    /// <param name="ct">Cancellation token.</param>
    Task EnsureUserAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct);

    Task<int> GetBalanceAsync(long userId, long balanceScopeId, CancellationToken ct);

    Task<bool> TryDebitAsync(long userId, long balanceScopeId, int amount, string reason, CancellationToken ct);

    async Task<EconomicsMutationResult> TryDebitOnceAsync(
        long userId,
        long balanceScopeId,
        int amount,
        string reason,
        string operationId,
        CancellationToken ct)
    {
        var applied = await TryDebitAsync(userId, balanceScopeId, amount, reason, ct);
        var balance = await GetBalanceAsync(userId, balanceScopeId, ct);
        return new EconomicsMutationResult(applied, !applied, balance);
    }

    Task DebitAsync(long userId, long balanceScopeId, int amount, string reason, CancellationToken ct);

    Task CreditAsync(long userId, long balanceScopeId, int amount, string reason, CancellationToken ct);

    async Task<EconomicsMutationResult> CreditOnceAsync(
        long userId,
        long balanceScopeId,
        int amount,
        string reason,
        string operationId,
        CancellationToken ct)
    {
        await CreditAsync(userId, balanceScopeId, amount, reason, ct);
        var balance = await GetBalanceAsync(userId, balanceScopeId, ct);
        return new EconomicsMutationResult(true, false, balance);
    }

    Task AdjustUncheckedAsync(long userId, long balanceScopeId, int delta, CancellationToken ct);

    Task<LedgerRevertResult> RevertLedgerEntryAsync(long economicsLedgerId, CancellationToken ct);

    Task<PeerTransferResult> TryPeerTransferAsync(
        long fromUserId,
        long toUserId,
        long balanceScopeId,
        int debitFromSender,
        int creditToRecipient,
        string senderReason,
        string recipientReason,
        CancellationToken ct);

    Task<PeerTransferResult> TryPeerTransferOnceAsync(
        long fromUserId,
        long toUserId,
        long balanceScopeId,
        int debitFromSender,
        int creditToRecipient,
        string senderReason,
        string recipientReason,
        string operationId,
        CancellationToken ct) =>
        TryPeerTransferAsync(
            fromUserId,
            toUserId,
            balanceScopeId,
            debitFromSender,
            creditToRecipient,
            senderReason,
            recipientReason,
            ct);
}
