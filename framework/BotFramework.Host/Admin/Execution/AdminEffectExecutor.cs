using System.Text.Json;
using BotFramework.Sdk.Admin.Execution;
using BotFramework.Contracts.Tenancy;
using BotFramework.Host.Contracts.Economics;
using Dapper;
using Npgsql;

namespace BotFramework.Host.Admin.Execution;

internal sealed class AdminEffectExecutor(
    INpgsqlConnectionFactory connections,
    IEnumerable<IAdminEffectHandler> handlers,
    IWalletAtomicExecutionService wallet) : IAdminEffectExecutor
{
    private readonly Dictionary<Type, IAdminEffectHandler> _handlers = handlers
        .GroupBy(static handler => handler.EffectType)
        .ToDictionary(static group => group.Key, static group => group.Single());

    public async Task<TResult> ExecuteAsync<TResult>(
        AdminExecutionEnvelope envelope,
        AdminEffectPlan<TResult> plan,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(envelope.Action))
            throw new ArgumentException("Admin audit action is required.", nameof(envelope));

        await using var connection = await connections.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var context = new PostgresAdminExecutionContext(
            connection,
            transaction,
            envelope.Actor,
            envelope.Action,
            wallet,
            envelope.TenantContext);

        try
        {
            foreach (var effect in plan.Effects)
            {
                if (!_handlers.TryGetValue(effect.GetType(), out var handler))
                    throw new InvalidOperationException($"No admin effect handler is registered for '{effect.GetType().FullName}'.");

                await handler.ApplyAsync(effect, context, ct);
            }

            await context.ExecuteAsync(
                """
                INSERT INTO admin_audit (
                    actor_id, actor_name, action, details, occurred_at, tenant_key, scope_key)
                SELECT @actorId, @actorName, @action, @details::jsonb, now(),
                       t.tenant_key, s.scope_key
                FROM (SELECT @tenantId::text AS tenant_id, @scopeId::text AS scope_id) request
                LEFT JOIN tenants t ON t.tenant_id = request.tenant_id
                LEFT JOIN tenant_scopes s
                    ON s.tenant_key = t.tenant_key AND s.scope_id = request.scope_id
                """,
                new
                {
                    actorId = envelope.Actor.Id,
                    actorName = envelope.Actor.Name,
                    envelope.Action,
                    details = JsonSerializer.Serialize(envelope.AuditDetails),
                    tenantId = envelope.TenantContext?.TenantId.Value,
                    scopeId = envelope.TenantContext?.ScopeId.Value,
                },
                ct);

            await transaction.CommitAsync(ct);
            return plan.ResultFactory is { } resultFactory
                ? resultFactory(context.Outputs)
                : plan.Result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private sealed class PostgresAdminExecutionContext(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AdminActor actor,
        string action,
        IWalletAtomicExecutionService wallet,
        TenantContext? tenantContext) : IAdminExecutionContext
    {
        private readonly Dictionary<string, object?> _outputs = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, object?> Outputs => _outputs;

        public AdminActor Actor => actor;

        public string Action => action;
        public IWalletAtomicExecutionService Wallet { get; } = wallet;
        public TenantContext? TenantContext { get; } = tenantContext;

        public Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: ct));

        public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct) =>
            await connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(sql, parameters, transaction, cancellationToken: ct));

        public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct) =>
            (await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, transaction, cancellationToken: ct))).AsList();

        public void SetOutput(string key, object? value)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Output key is required.", nameof(key));
            _outputs[key] = value;
        }
    }
}
