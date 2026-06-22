namespace BotFramework.Host.Commands.RateLimiting;

public interface IRateLimitPolicy
{
    Task<bool> TryAcquireAsync(RateLimitKey key, CancellationToken ct);
}
