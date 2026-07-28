using Dapper;
using Npgsql;

namespace BotFramework.Host.Execution;

internal sealed class PostgresGameExecutionSession : IGameExecutionSession
{
    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;
    private bool completed;

    private PostgresGameExecutionSession(NpgsqlConnection connection, NpgsqlTransaction transaction)
    {
        this.connection = connection;
        this.transaction = transaction;
    }

    public System.Data.Common.DbConnection Connection => connection;

    public System.Data.Common.DbTransaction Transaction => transaction;

    public static async Task<PostgresGameExecutionSession> BeginAsync(
        INpgsqlConnectionFactory connections,
        CancellationToken ct)
    {
        var openedConnection = await connections.OpenAsync(ct);
        try
        {
            var openedTransaction = await openedConnection.BeginTransactionAsync(ct);
            return new PostgresGameExecutionSession(openedConnection, openedTransaction);
        }
        catch
        {
            await openedConnection.DisposeAsync();
            throw;
        }
    }

    public async Task AcquireLocksAsync(IEnumerable<string> lockKeys, CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(completed, this);
        ArgumentNullException.ThrowIfNull(lockKeys);

        var orderedKeys = lockKeys
            .Select(ValidateLockKey)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

        foreach (var lockKey in orderedKeys)
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "SELECT pg_advisory_xact_lock(hashtextextended(@lockKey, 0))",
                new { lockKey },
                transaction,
                cancellationToken: ct));
        }
    }

    public async Task CommitAsync(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(completed, this);
        await transaction.CommitAsync(ct);
        completed = true;
    }

    public async Task RollbackAsync(CancellationToken ct)
    {
        if (completed) return;
        await transaction.RollbackAsync(ct);
        completed = true;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (!completed)
            {
                try
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                catch (InvalidOperationException)
                {
                    // The server may already have aborted the transaction.
                }
                catch (NpgsqlException)
                {
                    // A broken connection already implies rollback on the server.
                }
            }
        }
        finally
        {
            await transaction.DisposeAsync();
            await connection.DisposeAsync();
            completed = true;
        }
    }

    private static string ValidateLockKey(string lockKey)
    {
        if (string.IsNullOrWhiteSpace(lockKey))
            throw new ArgumentException("Execution lock keys cannot be empty.", nameof(lockKey));
        return lockKey;
    }
}
