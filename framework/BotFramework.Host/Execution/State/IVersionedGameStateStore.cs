using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

public interface IVersionedGameStateStore<TCommand, TState> : IGameStateStore<TCommand, TState>
{
    Task SaveVersionedAsync(
        TCommand command,
        TState state,
        long expectedRevision,
        IGameExecutionContext context,
        CancellationToken ct);
}
