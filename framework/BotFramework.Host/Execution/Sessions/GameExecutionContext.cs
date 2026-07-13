using Dapper;

namespace BotFramework.Host.Execution;

internal sealed class GameExecutionContext(IGameExecutionSession session) : IGameExecutionContext
{
    public Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct) =>
        session.Connection.ExecuteAsync(new CommandDefinition(
            sql,
            parameters,
            session.Transaction,
            cancellationToken: ct));

    public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct) =>
        session.Connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(
            sql,
            parameters,
            session.Transaction,
            cancellationToken: ct));
}
