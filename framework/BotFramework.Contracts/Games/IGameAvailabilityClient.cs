namespace BotFramework.Contracts.Games;

/// <summary>Transport-neutral fast-path client. Its answer never replaces the backend's authoritative check.</summary>
public interface IGameAvailabilityClient
{
    Task<GameAvailability> GetAsync(long chatId, string gameId, CancellationToken ct = default);
}
