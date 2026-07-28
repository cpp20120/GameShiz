namespace BotFramework.Contracts.RateLimiting;

/// <summary>
/// Shared limiter boundary used by HTTP, Telegram, Discord, and command
/// pipelines. Implementations must evaluate all applicable dimensions before
/// applying a lease so a rejected request has no game side effects.
/// </summary>
public interface IRateLimiter
{
    ValueTask<RateLimitDecision> CheckAsync(RateLimitRequest request, CancellationToken cancellationToken = default);
}
