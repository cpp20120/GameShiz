namespace BotFramework.Host.Execution;

public interface IAtomicEffectContext
{
    string? OperationId => null;
    IWalletAtomicExecutionService? Wallet => null;
    IEconomicsService? Economics => null;

    Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct);

    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct);

    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct);

    void SetOutput(string key, object? value);
}