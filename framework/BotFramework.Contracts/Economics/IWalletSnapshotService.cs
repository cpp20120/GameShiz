namespace BotFramework.Host.Contracts.Economics;

/// <summary>
/// Returns the current wallet balance while ensuring the account exists.
/// Implementations own the atomic create-or-update/read boundary so callers
/// do not need separate ensure and balance RPCs on a game hot path.
/// </summary>
public interface IWalletSnapshotService
{
    Task<int> EnsureAndGetBalanceAsync(
        long userId,
        long balanceScopeId,
        string displayName,
        CancellationToken ct);
}
