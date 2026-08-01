namespace BotFramework.Host.Execution;

/// <summary>
/// Reads a framework-owned aggregate snapshot outside an atomic command.
/// Tenant selection is handled by the framework implementation and is not a
/// concern of game modules.
/// </summary>
public interface IGameAggregateStateReader
{
    Task<string?> LoadJsonAsync(
        string gameId,
        string aggregateId,
        CancellationToken ct);
}
