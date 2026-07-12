namespace BotFramework.Host.Execution;

public interface IGameStateStore<TCommand, TState>
{
    Task<TState> LoadAsync(TCommand command, IGameExecutionContext context, CancellationToken ct);

    Task SaveAsync(TCommand command, TState state, IGameExecutionContext context, CancellationToken ct);
}
