namespace BotFramework.Host.Commands;

public interface IRateLimitPolicy
{
    Task<bool> TryAcquireAsync(RateLimitKey key, CancellationToken ct);
}
