using System.Text.Json;
using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

/// <summary>
/// Framework-owned JSONB persistence for versioned aggregates. A game may replace it with a
/// specialized transaction-aware store when it needs a relational state model.
/// </summary>
public sealed class PostgresJsonGameStateStore<TCommand, TState, TResult>(
    GameExecutionDescriptor<TCommand, TState, TResult> descriptor)
    : IVersionedGameStateStore<TCommand, TState>
    where TState : IVersionedGameState
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string StateType = typeof(TState).FullName ?? typeof(TState).Name;

    public async Task<TState> LoadAsync(
        TCommand command,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        var row = await context.QuerySingleOrDefaultAsync<StateRow>(
            """
            SELECT state_type AS StoredStateType,
                   version AS Version,
                   state::text AS StateJson
            FROM game_aggregate_states
            WHERE game_id = @gameId AND aggregate_id = @aggregateId
            FOR UPDATE
            """,
            Identity(command),
            ct).ConfigureAwait(false);
        if (row is null)
        {
            var initial = descriptor.CreateInitialState(command);
            if (initial.Revision != 0)
                throw new InvalidOperationException("A new framework game aggregate must start at revision zero.");
            return initial;
        }

        if (!string.Equals(row.StoredStateType, StateType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Stored aggregate state type '{row.StoredStateType}' does not match '{StateType}'.");
        }

        var state = JsonSerializer.Deserialize<TState>(row.StateJson, JsonOptions)
            ?? throw new InvalidOperationException($"Stored aggregate '{descriptor.AggregateId(command)}' has null state.");
        if (state.Revision != row.Version)
            throw new InvalidOperationException("Stored aggregate revision does not match its serialized state.");
        return state;
    }

    public Task SaveAsync(
        TCommand command,
        TState state,
        IGameExecutionContext context,
        CancellationToken ct) =>
        SaveVersionedAsync(command, state, checked(state.Revision - 1), context, ct);

    public async Task SaveVersionedAsync(
        TCommand command,
        TState state,
        long expectedRevision,
        IGameExecutionContext context,
        CancellationToken ct)
    {
        if (state.Revision != checked(expectedRevision + 1))
            throw new InvalidOperationException("Versioned state must advance exactly one revision.");

        var identity = Identity(command);
        var args = new
        {
            identity.GameId,
            identity.AggregateId,
            stateType = StateType,
            expectedRevision,
            revision = state.Revision,
            state = JsonSerializer.Serialize(state, JsonOptions),
        };
        var affected = await context.ExecuteAsync(
            """
            UPDATE game_aggregate_states
            SET state_type = @stateType,
                version = @revision,
                state = CAST(@state AS jsonb),
                updated_at = now()
            WHERE game_id = @gameId
              AND aggregate_id = @aggregateId
              AND version = @expectedRevision
            """,
            args,
            ct).ConfigureAwait(false);
        if (affected == 1)
            return;

        if (expectedRevision == 0)
        {
            affected = await context.ExecuteAsync(
                """
                INSERT INTO game_aggregate_states (
                    game_id, aggregate_id, state_type, version, state, updated_at)
                VALUES (@gameId, @aggregateId, @stateType, @revision, CAST(@state AS jsonb), now())
                ON CONFLICT (game_id, aggregate_id) DO NOTHING
                """,
                args,
                ct).ConfigureAwait(false);
            if (affected == 1)
                return;
        }

        throw new GameStateConcurrencyException(
            descriptor.GameId,
            descriptor.AggregateId(command),
            expectedRevision);
    }

    private StateIdentity Identity(TCommand command) =>
        new(descriptor.GameId, descriptor.AggregateId(command));

    private sealed record StateIdentity(string GameId, string AggregateId);
    private sealed record StateRow(string StoredStateType, long Version, string StateJson);
}
