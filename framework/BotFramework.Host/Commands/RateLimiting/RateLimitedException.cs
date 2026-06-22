namespace BotFramework.Host.Commands.RateLimiting;

public sealed class RateLimitedException(RateLimitKey key)
    : Exception($"rate-limited: user={key.UserId} command={key.CommandType}");
