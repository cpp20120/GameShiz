using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.Darts.Application.Execution;

public sealed class DartsNoGameStateStore : IGameStateStore<DartsQuickThrowCommand, NoGameState>
{
    public Task<NoGameState> LoadAsync(
        DartsQuickThrowCommand command,
        IGameExecutionContext context,
        CancellationToken ct) => Task.FromResult(default(NoGameState));

    public Task SaveAsync(
        DartsQuickThrowCommand command,
        NoGameState state,
        IGameExecutionContext context,
        CancellationToken ct) => Task.CompletedTask;
}
