using System.Text.Json;
using BotFramework.Sdk.Admin.Execution;
using BotFramework.Host.Contracts.Economics;
using Dapper;
using Npgsql;

namespace BotFramework.Host.Admin.Execution;

public interface IAdminEffectExecutor
{
    Task<TResult> ExecuteAsync<TResult>(
        AdminExecutionEnvelope envelope,
        AdminEffectPlan<TResult> plan,
        CancellationToken ct);
}

public interface IAdminExecutionContext
{
    IWalletAtomicExecutionService? Wallet => null;

    AdminActor Actor { get; }

    string Action { get; }

    Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct);

    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct);

    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct);

    void SetOutput(string key, object? value);
}

public interface IAdminEffectHandler
{
    Type EffectType { get; }
    Task ApplyAsync(IAdminEffect effect, IAdminExecutionContext context, CancellationToken ct);
}

public abstract class AdminEffectHandler<TEffect> : IAdminEffectHandler
    where TEffect : class, IAdminEffect
{
    public Type EffectType => typeof(TEffect);

    public Task ApplyAsync(IAdminEffect effect, IAdminExecutionContext context, CancellationToken ct) =>
        ApplyAsync((TEffect)effect, context, ct);

    protected abstract Task ApplyAsync(TEffect effect, IAdminExecutionContext context, CancellationToken ct);
}

internal sealed class AdminEffectExecutor(
    INpgsqlConnectionFactory connections,
    IEnumerable<IAdminEffectHandler> handlers,
    IWalletAtomicExecutionService wallet) : IAdminEffectExecutor
{
    private readonly IReadOnlyDictionary<Type, IAdminEffectHandler> handlers = handlers
        .GroupBy(static handler => handler.EffectType)
        .ToDictionary(static group => group.Key, static group => group.Single());

    public async Task<TResult> ExecuteAsync<TResult>(
        AdminExecutionEnvelope envelope,
        AdminEffectPlan<TResult> plan,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(envelope.Action))
            throw new ArgumentException("Admin audit action is required.", nameof(envelope));

        await using var connection = await connections.OpenAsync(ct).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(ct).ConfigureAwait(false);
        var context = new PostgresAdminExecutionContext(connection, transaction, envelope.Actor, envelope.Action, wallet);

        try
        {
            foreach (var effect in plan.Effects)
            {
                if (!handlers.TryGetValue(effect.GetType(), out var handler))
                    throw new InvalidOperationException($"No admin effect handler is registered for '{effect.GetType().FullName}'.");

                await handler.ApplyAsync(effect, context, ct).ConfigureAwait(false);
            }

            await context.ExecuteAsync(
                """
                INSERT INTO admin_audit (actor_id, actor_name, action, details, occurred_at)
                VALUES (@actorId, @actorName, @action, @details::jsonb, now())
                """,
                new
                {
                    actorId = envelope.Actor.Id,
                    actorName = envelope.Actor.Name,
                    envelope.Action,
                    details = JsonSerializer.Serialize(envelope.AuditDetails),
                },
                ct).ConfigureAwait(false);

            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return plan.ResultFactory is { } resultFactory
                ? resultFactory(context.Outputs)
                : plan.Result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private sealed class PostgresAdminExecutionContext(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        AdminActor actor,
        string action,
        IWalletAtomicExecutionService wallet) : IAdminExecutionContext
    {
        private readonly Dictionary<string, object?> outputs = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, object?> Outputs => outputs;

        public AdminActor Actor => actor;

        public string Action => action;
        public IWalletAtomicExecutionService Wallet { get; } = wallet;

        public Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct) =>
            connection.ExecuteAsync(new CommandDefinition(sql, parameters, transaction, cancellationToken: ct));

        public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct) =>
            await connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(sql, parameters, transaction, cancellationToken: ct)).ConfigureAwait(false);

        public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct) =>
            (await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, transaction, cancellationToken: ct)).ConfigureAwait(false)).AsList();

        public void SetOutput(string key, object? value)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Output key is required.", nameof(key));
            outputs[key] = value;
        }
    }
}
