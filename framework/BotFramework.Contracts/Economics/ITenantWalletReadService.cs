using BotFramework.Contracts.Tenancy;

namespace BotFramework.Contracts.Economics;

/// <summary>Reads the canonical tenant wallet used by SDK 0.9 modules.</summary>
public interface ITenantWalletReadService
{
    Task<TenantWalletAccount?> GetAsync(
        TenantContext context,
        CancellationToken cancellationToken = default);
}
