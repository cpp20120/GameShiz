namespace BotFramework.Contracts.Caching;

/// <summary>
/// Small transport-neutral cache port for non-authoritative read models and
/// soft coordination. Callers must always remain correct on a cache miss.
/// </summary>
public interface ICacheStore
{
    Task<string?> GetStringAsync(string key, CancellationToken ct);

    Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken ct);
}
