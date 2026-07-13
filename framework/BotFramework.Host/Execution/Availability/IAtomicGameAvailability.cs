using BotFramework.Contracts.Games;

namespace BotFramework.Host.Execution;

internal interface IAtomicGameAvailability
{
    Task<GameAvailability> GetAsync(
        long chatId,
        string gameId,
        IGameExecutionSession session,
        CancellationToken ct);
}
