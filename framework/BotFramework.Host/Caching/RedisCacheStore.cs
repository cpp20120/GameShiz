using BotFramework.Contracts.Caching;
using StackExchange.Redis;

namespace BotFramework.Host.Caching;

/// <summary>Best-effort Redis implementation for cache-only callers.</summary>
public sealed partial class RedisCacheStore(
    IConnectionMultiplexer redis,
    ILogger<RedisCacheStore> logger) : ICacheStore
{
    public async Task<string?> GetStringAsync(string key, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            var value = await redis.GetDatabase().StringGetAsync(key);
            return value.HasValue ? value.ToString() : null;
        }
        catch (RedisException ex)
        {
            LogReadFailed(ex);
            return null;
        }
    }

    public async Task SetStringAsync(string key, string value, TimeSpan ttl, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try
        {
            await redis.GetDatabase().StringSetAsync(key, value, ttl);
        }
        catch (RedisException ex)
        {
            LogWriteFailed(ex);
        }
    }

    [LoggerMessage(LogLevel.Debug, "cache.redis_read_failed")]
    private partial void LogReadFailed(Exception exception);

    [LoggerMessage(LogLevel.Debug, "cache.redis_write_failed")]
    private partial void LogWriteFailed(Exception exception);
}
