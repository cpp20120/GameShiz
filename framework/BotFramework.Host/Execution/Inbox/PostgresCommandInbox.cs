using System.Text.Json;
using BotFramework.Sdk.Execution;
using Dapper;

namespace BotFramework.Host.Execution;

internal sealed class PostgresCommandInbox : ICommandInbox
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<CommandInboxResult<TResult>> GetOrBeginAsync<TResult>(
        string commandId,
        string gameId,
        string aggregateId,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        ValidateIdentifier(commandId, nameof(commandId), 256);
        ValidateIdentifier(gameId, nameof(gameId), 100);
        ValidateIdentifier(aggregateId, nameof(aggregateId), 256);
        ArgumentNullException.ThrowIfNull(session);

        const string insertSql = """
            INSERT INTO game_command_idempotency (
                idempotency_key,
                status,
                game_id,
                aggregate_id,
                started_at)
            VALUES (@commandId, 'pending', @gameId, @aggregateId, now())
            ON CONFLICT (idempotency_key) DO NOTHING
            """;
        var inserted = await session.Connection.ExecuteAsync(new CommandDefinition(
            insertSql,
            new { commandId, gameId, aggregateId },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);

        const string selectSql = """
            SELECT status AS Status,
                   game_id AS GameId,
                   aggregate_id AS AggregateId,
                   result_type AS ResultType,
                   result_json::text AS ResultJson
            FROM game_command_idempotency
            WHERE idempotency_key = @commandId
            FOR UPDATE
            """;
        var row = await session.Connection.QuerySingleAsync<InboxRow>(new CommandDefinition(
            selectSql,
            new { commandId },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);

        if (inserted == 1)
            return new CommandInboxResult<TResult>(CommandInboxStatus.New, default);

        if (!string.Equals(row.GameId, gameId, StringComparison.Ordinal) ||
            !string.Equals(row.AggregateId, aggregateId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Command '{commandId}' was already registered for another game aggregate.");
        }

        if (!string.Equals(row.Status, "completed", StringComparison.Ordinal))
            throw new InvalidOperationException($"Command '{commandId}' has an incomplete inbox entry.");

        var expectedType = typeof(TResult).FullName ?? typeof(TResult).Name;
        if (!string.Equals(row.ResultType, expectedType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Command '{commandId}' contains result type '{row.ResultType}', expected '{expectedType}'.");
        }

        if (row.ResultJson is null)
            throw new InvalidOperationException($"Completed command '{commandId}' has no stored result.");

        var result = JsonSerializer.Deserialize<TResult>(row.ResultJson, JsonOptions);
        return new CommandInboxResult<TResult>(CommandInboxStatus.Completed, result);
    }

    public async Task CompleteAsync<TResult>(
        string commandId,
        TResult result,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        ValidateIdentifier(commandId, nameof(commandId), 256);
        ArgumentNullException.ThrowIfNull(session);

        var resultType = typeof(TResult).FullName ?? typeof(TResult).Name;
        var resultJson = JsonSerializer.Serialize(result, JsonOptions);
        const string sql = """
            UPDATE game_command_idempotency
            SET status = 'completed',
                result_type = @resultType,
                result_json = CAST(@resultJson AS jsonb),
                completed_at = now(),
                error = NULL
            WHERE idempotency_key = @commandId
              AND status = 'pending'
            """;
        var affected = await session.Connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { commandId, resultType, resultJson },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);

        if (affected != 1)
            throw new InvalidOperationException($"Pending command '{commandId}' was not found.");
    }

    public async Task StoreEntropyAsync(
        string commandId,
        EntropyValue entropy,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        ValidateIdentifier(commandId, nameof(commandId), 256);
        ArgumentNullException.ThrowIfNull(entropy);
        ArgumentNullException.ThrowIfNull(session);

        var entropyJson = JsonSerializer.Serialize(entropy.Values, JsonOptions);
        var affected = await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE game_command_idempotency
            SET entropy_json = CAST(@entropyJson AS jsonb)
            WHERE idempotency_key = @commandId
              AND status = 'pending'
            """,
            new { commandId, entropyJson },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);
        if (affected != 1)
            throw new InvalidOperationException($"Pending command '{commandId}' was not found.");
    }

    private static void ValidateIdentifier(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Identifier is required.", parameterName);
        if (value.Length > maxLength)
            throw new ArgumentOutOfRangeException(parameterName, value.Length, $"Identifier must be <= {maxLength} characters.");
    }

    private sealed record InboxRow(
        string Status,
        string? GameId,
        string? AggregateId,
        string? ResultType,
        string? ResultJson);
}
