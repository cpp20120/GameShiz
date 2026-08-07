namespace BotFramework.Contracts.Caching;

/// <summary>Optional invalidation port for cache implementations that support key deletion.</summary>
public interface ICacheStoreInvalidator
{
    Task RemoveStringAsync(string key, CancellationToken ct);
}
