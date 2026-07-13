using BotFramework.Sdk.Execution;
using Dapper;

namespace BotFramework.Host.Execution;

public interface IAtomicEffectExecutor
{
    Task<TResult> ExecuteAsync<TResult>(
        AtomicEffectExecutionEnvelope envelope,
        AtomicEffectPlan<TResult> plan,
        CancellationToken ct);
}

public interface IAtomicEffectContext
{
    Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct);

    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct);

    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct);

    void SetOutput(string key, object? value);
}

public interface IAtomicEffectHandler
{
    Type EffectType { get; }

    Task ApplyAsync(IAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct);
}

public abstract class AtomicEffectHandler<TEffect> : IAtomicEffectHandler
    where TEffect : class, IAtomicEffect
{
    public Type EffectType => typeof(TEffect);

    public Task ApplyAsync(IAtomicEffect effect, IAtomicEffectContext context, CancellationToken ct) =>
        ApplyAsync((TEffect)effect, context, ct);

    protected abstract Task ApplyAsync(TEffect effect, IAtomicEffectContext context, CancellationToken ct);
}

internal sealed class AtomicEffectExecutor(
    IGameExecutionSessionFactory sessions,
    ICommandInbox inbox,
    IEnumerable<IAtomicEffectHandler> handlers) : IAtomicEffectExecutor
{
    private readonly IReadOnlyDictionary<Type, IAtomicEffectHandler> handlers = handlers
        .GroupBy(static handler => handler.EffectType)
        .ToDictionary(static group => group.Key, static group => group.Single());

    public async Task<TResult> ExecuteAsync<TResult>(
        AtomicEffectExecutionEnvelope envelope,
        AtomicEffectPlan<TResult> plan,
        CancellationToken ct)
    {
        Validate(envelope, plan);
        await using var session = await sessions.BeginAsync(ct).ConfigureAwait(false);
        var committed = false;
        try
        {
            await session.AcquireLocksAsync(envelope.LockKeys, ct).ConfigureAwait(false);
            var existing = await inbox.GetOrBeginAsync<TResult>(
                envelope.CommandId,
                envelope.GameId,
                envelope.AggregateId,
                session,
                ct).ConfigureAwait(false);
            if (existing.Status == CommandInboxStatus.Completed)
            {
                await session.CommitAsync(ct).ConfigureAwait(false);
                committed = true;
                return existing.Result!;
            }

            var context = new PostgresAtomicEffectContext(session);
            foreach (var effect in plan.Effects)
            {
                if (!handlers.TryGetValue(effect.GetType(), out var handler))
                    throw new InvalidOperationException($"No atomic effect handler is registered for '{effect.GetType().FullName}'.");
                await handler.ApplyAsync(effect, context, ct).ConfigureAwait(false);
            }

            var result = plan.ResultFactory is { } factory
                ? factory(context.Outputs)
                : plan.Result;
            await inbox.CompleteAsync(envelope.CommandId, result, session, ct).ConfigureAwait(false);
            await session.CommitAsync(ct).ConfigureAwait(false);
            committed = true;
            return result;
        }
        catch
        {
            if (!committed)
                await session.RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    private static void Validate<TResult>(
        AtomicEffectExecutionEnvelope envelope,
        AtomicEffectPlan<TResult> plan)
    {
        if (string.IsNullOrWhiteSpace(envelope.GameId)
            || string.IsNullOrWhiteSpace(envelope.CommandId)
            || string.IsNullOrWhiteSpace(envelope.AggregateId))
            throw new ArgumentException("Game, command and aggregate identifiers are required.", nameof(envelope));
        if (envelope.LockKeys is null || envelope.LockKeys.Count == 0)
            throw new ArgumentException("At least one stable lock key is required.", nameof(envelope));
        if (plan.Effects is null || plan.Effects.Any(static effect => effect is null))
            throw new ArgumentException("Atomic effect plan cannot contain null effects.", nameof(plan));
    }

    private sealed class PostgresAtomicEffectContext(IGameExecutionSession session) : IAtomicEffectContext
    {
        private readonly Dictionary<string, object?> outputs = new(StringComparer.Ordinal);

        public IReadOnlyDictionary<string, object?> Outputs => outputs;

        public Task<int> ExecuteAsync(string sql, object? parameters, CancellationToken ct) =>
            session.Connection.ExecuteAsync(new CommandDefinition(
                sql, parameters, session.Transaction, cancellationToken: ct));

        public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? parameters, CancellationToken ct) =>
            await session.Connection.QuerySingleOrDefaultAsync<T>(new CommandDefinition(
                sql, parameters, session.Transaction, cancellationToken: ct)).ConfigureAwait(false);

        public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters, CancellationToken ct) =>
            (await session.Connection.QueryAsync<T>(new CommandDefinition(
                sql, parameters, session.Transaction, cancellationToken: ct)).ConfigureAwait(false)).AsList();

        public void SetOutput(string key, object? value)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Output key is required.", nameof(key));
            outputs[key] = value;
        }
    }
}
