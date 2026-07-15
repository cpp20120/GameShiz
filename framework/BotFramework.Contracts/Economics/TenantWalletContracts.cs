using BotFramework.Contracts.Tenancy;

namespace BotFramework.Contracts.Economics;

/// <summary>
/// Scope-local wallet owned by a tenant-aware SDK module. Platform-specific
/// numeric ids never cross this contract.
/// </summary>
public sealed record TenantWalletAccount(
    TenantId TenantId,
    ScopeId ScopeId,
    PlayerId PlayerId,
    long Balance,
    long Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Reads the canonical tenant wallet used by SDK 0.9 modules.</summary>
public interface ITenantWalletReadService
{
    Task<TenantWalletAccount?> GetAsync(
        TenantContext context,
        CancellationToken cancellationToken = default);
}
