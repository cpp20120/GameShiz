using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Contracts.Economics;
using BotFramework.Sdk.Admin.Execution;

namespace BotFramework.Host.Admin.Execution;

public interface IAdminExecutionContext
{
    IWalletAtomicExecutionService? Wallet => null;
    TenantContext? TenantContext => null;

    AdminActor Actor { get; }
    string Action { get; }

    Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct);
    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct);
    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct);
    void SetOutput(string key, object? value);
}
