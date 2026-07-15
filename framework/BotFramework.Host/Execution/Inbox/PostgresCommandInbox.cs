using System.Text.Json;
using BotFramework.Contracts.Messaging;
using BotFramework.Contracts.Tenancy;
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

        if (CurrentTenantContext() is { } tenant)
            return await GetOrBeginTenantAsync<TResult>(commandId, gameId, aggregateId, tenant, session, ct).ConfigureAwait(false);

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

        if (CurrentTenantContext() is { } tenant)
        {
            await CompleteTenantAsync(commandId, result, tenant, session, ct).ConfigureAwait(false);
            return;
        }

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

        if (CurrentTenantContext() is { } tenant)
        {
            await session.Connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE tenant_idempotency_keys
                SET entropy_json = CAST(@entropyJson AS jsonb)
                WHERE tenant_key = (SELECT tenant_key FROM tenants WHERE tenant_id = @tenantId)
                  AND scope_key = (
                      SELECT scope_key FROM tenant_scopes
                      WHERE tenant_key = (SELECT tenant_key FROM tenants WHERE tenant_id = @tenantId)
                        AND scope_id = @scopeId)
                  AND idempotency_key = @commandId
                  AND status = 'pending'
                """,
                new
                {
                    commandId,
                    entropyJson = JsonSerializer.Serialize(entropy.Values, JsonOptions),
                    tenantId = tenant.TenantId.Value,
                    scopeId = tenant.ScopeId.Value,
                },
                session.Transaction,
                cancellationToken: ct)).ConfigureAwait(false);
            return;
        }

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

    private static TenantContext? CurrentTenantContext()
    {
        var metadata = RequestMetadataContext.TryGetCurrent();
        return metadata?.TenantContext
            ?? (metadata?.Tenant is { } tenant && metadata.TypedScope is { } scope
                ? TenantContext.Create(tenant, scope, metadata.Player, metadata.Channel,
                    metadata.TypedRequestId, metadata.TypedCorrelationId)
                : null);
    }

    private static async Task<CommandInboxResult<TResult>> GetOrBeginTenantAsync<TResult>(
        string commandId,
        string gameId,
        string aggregateId,
        TenantContext tenant,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        var requestId = tenant.RequestId.Value;
        var inserted = await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO tenant_idempotency_keys (
                tenant_key, scope_key, player_id, idempotency_key, request_id,
                status, game_id, aggregate_id, created_at)
            SELECT t.tenant_key, s.scope_key, @playerId, @commandId, @requestId,
                   'pending', @gameId, @aggregateId, now()
            FROM tenants t
            JOIN tenant_scopes s ON s.tenant_key = t.tenant_key AND s.scope_id = @scopeId
            WHERE t.tenant_id = @tenantId
            ON CONFLICT (tenant_key, scope_key, idempotency_key) DO NOTHING
            """,
            new
            {
                tenantId = tenant.TenantId.Value,
                scopeId = tenant.ScopeId.Value,
                playerId = tenant.PlayerId?.Value,
                commandId,
                requestId,
                gameId,
                aggregateId,
            },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);

        var row = await session.Connection.QuerySingleAsync<TenantInboxRow>(new CommandDefinition(
            """
            SELECT status AS Status,
                   game_id AS GameId,
                   aggregate_id AS AggregateId,
                   result_type AS ResultType,
                   response_payload::text AS ResultJson
            FROM tenant_idempotency_keys
            WHERE tenant_key = (SELECT tenant_key FROM tenants WHERE tenant_id = @tenantId)
              AND scope_key = (
                  SELECT scope_key FROM tenant_scopes
                  WHERE tenant_key = (SELECT tenant_key FROM tenants WHERE tenant_id = @tenantId)
                    AND scope_id = @scopeId)
              AND idempotency_key = @commandId
            FOR UPDATE
            """,
            new { tenantId = tenant.TenantId.Value, scopeId = tenant.ScopeId.Value, commandId },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);

        if (inserted == 1)
            return new CommandInboxResult<TResult>(CommandInboxStatus.New, default);

        ValidateExisting(row.GameId, row.AggregateId, gameId, aggregateId, commandId);
        if (!string.Equals(row.Status, "completed", StringComparison.Ordinal))
            throw new InvalidOperationException($"Command '{commandId}' has an incomplete inbox entry.");

        var expectedType = typeof(TResult).FullName ?? typeof(TResult).Name;
        if (!string.Equals(row.ResultType, expectedType, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Command '{commandId}' contains result type '{row.ResultType}', expected '{expectedType}'.");
        if (row.ResultJson is null)
            throw new InvalidOperationException($"Completed command '{commandId}' has no stored result.");

        return new CommandInboxResult<TResult>(
            CommandInboxStatus.Completed,
            JsonSerializer.Deserialize<TResult>(row.ResultJson, JsonOptions));
    }

    private static async Task CompleteTenantAsync<TResult>(
        string commandId,
        TResult result,
        TenantContext tenant,
        IGameExecutionSession session,
        CancellationToken ct)
    {
        var affected = await session.Connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE tenant_idempotency_keys
            SET status = 'completed',
                result_type = @resultType,
                response_status = 200,
                response_payload = CAST(@resultJson AS jsonb),
                completed_at = now(),
                error = NULL
            WHERE tenant_key = (SELECT tenant_key FROM tenants WHERE tenant_id = @tenantId)
              AND scope_key = (
                  SELECT scope_key FROM tenant_scopes
                  WHERE tenant_key = (SELECT tenant_key FROM tenants WHERE tenant_id = @tenantId)
                    AND scope_id = @scopeId)
              AND idempotency_key = @commandId
              AND status = 'pending'
            """,
            new
            {
                tenantId = tenant.TenantId.Value,
                scopeId = tenant.ScopeId.Value,
                commandId,
                resultType = typeof(TResult).FullName ?? typeof(TResult).Name,
                resultJson = JsonSerializer.Serialize(result, JsonOptions),
            },
            session.Transaction,
            cancellationToken: ct)).ConfigureAwait(false);
        if (affected != 1)
            throw new InvalidOperationException($"Pending tenant command '{commandId}' was not found.");
    }

    private static void ValidateExisting(
        string? storedGameId,
        string? storedAggregateId,
        string gameId,
        string aggregateId,
        string commandId)
    {
        if (!string.Equals(storedGameId, gameId, StringComparison.Ordinal)
            || !string.Equals(storedAggregateId, aggregateId, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"Command '{commandId}' was already registered for another game aggregate.");
    }

    private sealed record InboxRow(
        string Status,
        string? GameId,
        string? AggregateId,
        string? ResultType,
        string? ResultJson);

    private sealed record TenantInboxRow(
        string Status,
        string? GameId,
        string? AggregateId,
        string? ResultType,
        string? ResultJson);
}
