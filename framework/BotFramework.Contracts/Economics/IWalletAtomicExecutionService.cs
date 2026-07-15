namespace BotFramework.Host.Contracts.Economics;

public enum WalletBatchEffectKind
{
    Debit,
    Credit,
}

public sealed record WalletBatchEffect(
    WalletBatchEffectKind Kind,
    int Amount,
    string Reason);

public readonly record struct WalletBatchMutationResult(
    bool Applied,
    bool Rejected,
    int NewBalance);

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
