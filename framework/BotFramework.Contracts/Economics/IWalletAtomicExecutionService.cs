namespace BotFramework.Host.Contracts.Economics;

/// <summary>
/// The wallet-owned atomic boundary used by Backend in microservices mode.
/// The operation id is persisted by Wallet and makes retries safe after a
/// Backend transaction or network failure.
/// </summary>
public interface IWalletAtomicExecutionService
{
    Task EnsureUserAsync(
        long userId,
        long balanceScopeId,
        string displayName,
        CancellationToken ct);

    Task<int> GetBalanceAsync(
        long userId,
        long balanceScopeId,
        CancellationToken ct);

    Task<WalletBatchMutationResult> ApplyBatchAsync(
        long userId,
        long balanceScopeId,
        IReadOnlyList<WalletBatchEffect> effects,
        string operationId,
        CancellationToken ct);
}
