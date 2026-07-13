namespace BotFramework.Host.Execution;

internal interface IGameExecutionSessionFactory
{
    Task<IGameExecutionSession> BeginAsync(CancellationToken ct);
}
