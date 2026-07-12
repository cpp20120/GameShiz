namespace BotFramework.Host.Execution;

public interface IAtomicGameExecutor<TCommand, TState, TResult>
{
    Type StateType { get; }

    Task<TResult> ExecuteAsync(GameExecutionEnvelope<TCommand> envelope, CancellationToken ct);
}
