namespace BotFramework.Host.Contracts.Economics;

public sealed record WalletAccount(
    long UserId,
    long BalanceScopeId,
    string DisplayName,
    int Coins,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Read-only wallet account port for consumers outside the wallet owner.</summary>
public interface IWalletReadService
{
    Task<WalletAccount?> GetAsync(long userId, long balanceScopeId, CancellationToken ct);
    Task<IReadOnlyList<WalletAccount>> ListAsync(CancellationToken ct);
    Task<IReadOnlyList<WalletAccount>> ListByScopeAsync(long balanceScopeId, CancellationToken ct);
    Task<bool> ExistsAsync(long userId, CancellationToken ct);
}
