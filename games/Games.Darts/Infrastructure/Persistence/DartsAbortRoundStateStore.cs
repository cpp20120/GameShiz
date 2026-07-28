using BotFramework.Contracts.Messaging;
using BotFramework.Host.Execution;
using Games.Darts.Application.Execution;

namespace Games.Darts.Infrastructure.Persistence;

public sealed class DartsAbortRoundStateStore : IGameStateStore<DartsAbortRoundCommand, DartsQueuedState>
{
    public async Task<DartsQueuedState> LoadAsync(
        DartsAbortRoundCommand command, IGameExecutionContext context, CancellationToken ct) =>
        new(await DartsAtomicSql.ByIdAsync(command.RoundId, context, ct), 0);

    public Task SaveAsync(
        DartsAbortRoundCommand command, DartsQueuedState state, IGameExecutionContext context, CancellationToken ct) =>
        DartsAtomicSql.DeleteAsync(command.RoundId, context, ct);
}
