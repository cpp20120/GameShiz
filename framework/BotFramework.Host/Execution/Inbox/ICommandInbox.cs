namespace BotFramework.Host.Execution;

internal interface ICommandInbox
{
    Task<CommandInboxResult<TResult>> GetOrBeginAsync<TResult>(
        string commandId,
        string gameId,
        string aggregateId,
        IGameExecutionSession session,
        CancellationToken ct);

    Task CompleteAsync<TResult>(
        string commandId,
        TResult result,
        IGameExecutionSession session,
        CancellationToken ct);

    Task StoreEntropyAsync(
        string commandId,
        BotFramework.Sdk.Execution.EntropyValue entropy,
        IGameExecutionSession session,
        CancellationToken ct);
}
