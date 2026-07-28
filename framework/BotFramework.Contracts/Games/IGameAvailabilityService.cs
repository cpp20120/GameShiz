namespace BotFramework.Contracts.Games;

/// <summary>Authoritative backend service. Mutating game handlers must check it immediately before writes.</summary>
public interface IGameAvailabilityService
{
    Task<GameAvailability> GetAsync(long chatId, string gameId, CancellationToken ct = default);
    Task<IReadOnlyList<GameAvailability>> ListOverridesAsync(long? chatId, CancellationToken ct = default);
    Task<GameAvailability> SetOverrideAsync(SetGameAvailability command, CancellationToken ct = default);
    Task RemoveOverrideAsync(long chatId, string gameId, long actorId, string actorName, CancellationToken ct = default);
}
