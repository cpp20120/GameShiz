namespace BotFramework.Contracts.Games;

public enum GameAvailabilitySource
{
    Configuration,
    ChatOverride,
    ConfigurationFallback,
}

public sealed record GameAvailability(
    long ChatId,
    string GameId,
    bool Enabled,
    GameAvailabilitySource Source,
    string? Reason = null,
    long? ChangedBy = null,
    DateTimeOffset? ChangedAt = null);

public sealed record SetGameAvailability(
    long ChatId,
    string GameId,
    bool Enabled,
    string Reason,
    long ActorId,
    string ActorName);

/// <summary>Authoritative backend service. Mutating game handlers must check it immediately before writes.</summary>
public interface IGameAvailabilityService
{
    Task<GameAvailability> GetAsync(long chatId, string gameId, CancellationToken ct = default);
    Task<IReadOnlyList<GameAvailability>> ListOverridesAsync(long? chatId, CancellationToken ct = default);
    Task<GameAvailability> SetOverrideAsync(SetGameAvailability command, CancellationToken ct = default);
    Task RemoveOverrideAsync(long chatId, string gameId, long actorId, string actorName, CancellationToken ct = default);
}

/// <summary>Transport-neutral fast-path client. Its answer never replaces the backend's authoritative check.</summary>
public interface IGameAvailabilityClient
{
    Task<GameAvailability> GetAsync(long chatId, string gameId, CancellationToken ct = default);
}
