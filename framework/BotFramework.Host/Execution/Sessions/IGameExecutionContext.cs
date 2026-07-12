namespace BotFramework.Host.Execution;

public interface IGameExecutionContext
{
    Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct);

    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct);
}
