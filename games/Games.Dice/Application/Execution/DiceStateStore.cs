using BotFramework.Host.Execution;
using BotFramework.Sdk.Execution;

namespace Games.Dice.Application.Execution;

public sealed class DiceStateStore : IGameStateStore<DiceCommand, NoGameState>
{
    public Task<NoGameState> LoadAsync(
        DiceCommand command,
        IGameExecutionContext context,
        CancellationToken ct) => Task.FromResult(default(NoGameState));

    public Task SaveAsync(
        DiceCommand command,
        NoGameState state,
        IGameExecutionContext context,
        CancellationToken ct) => Task.CompletedTask;
}
