namespace BotFramework.Host.Commands.RateLimiting;

public readonly record struct RateLimitKey(long UserId, string CommandType);
