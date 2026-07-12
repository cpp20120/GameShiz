namespace BotFramework.Host.Execution;

internal sealed class PostgresGameExecutionSessionFactory(INpgsqlConnectionFactory connections)
    : IGameExecutionSessionFactory
{
    public async Task<IGameExecutionSession> BeginAsync(CancellationToken ct) =>
        await PostgresGameExecutionSession.BeginAsync(connections, ct).ConfigureAwait(false);
}
