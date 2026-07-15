using BotFramework.Host.Contracts.Economics;

namespace BotFramework.Host.Execution;

/// <summary>
/// Monolith adapter for effect handlers that use the wallet boundary. It
/// delegates to the existing local service; the microservices adapter is the
/// Wallet gRPC proxy registered by the Backend host.
/// </summary>
internal sealed class LocalWalletAtomicExecutionService(IEconomicsService economics)
    : IWalletAtomicExecutionService
{
    public Task EnsureUserAsync(long userId, long balanceScopeId, string displayName, CancellationToken ct) =>
        economics.EnsureUserAsync(userId, balanceScopeId, displayName, ct);

    public Task<int> GetBalanceAsync(long userId, long balanceScopeId, CancellationToken ct) =>
        economics.GetBalanceAsync(userId, balanceScopeId, ct);

    public async Task<WalletBatchMutationResult> ApplyBatchAsync(
        long userId,
        long balanceScopeId,
        IReadOnlyList<WalletBatchEffect> effects,
        string operationId,
        CancellationToken ct)
    {
        var balance = await economics.GetBalanceAsync(userId, balanceScopeId, ct).ConfigureAwait(false);
        var applied = false;
        for (var index = 0; index < effects.Count; index++)
        {
            var effect = effects[index];
            var operation = $"{operationId}:{index}";
            EconomicsMutationResult result;
            if (effect.Kind == WalletBatchEffectKind.Debit)
            {
                result = await economics.TryDebitOnceAsync(
                    userId, balanceScopeId, effect.Amount, effect.Reason, operation, ct).ConfigureAwait(false);
            }
            else
            {
                result = await economics.CreditOnceAsync(
                    userId, balanceScopeId, effect.Amount, effect.Reason, operation, ct).ConfigureAwait(false);
            }

            balance = result.NewBalance;
            if (result.Rejected)
                return new WalletBatchMutationResult(applied, true, balance);
            applied |= result.Applied;
        }

        return new WalletBatchMutationResult(applied, false, balance);
    }
}
