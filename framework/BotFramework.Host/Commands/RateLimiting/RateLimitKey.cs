namespace BotFramework.Host.Commands;

public readonly record struct RateLimitKey(long UserId, string CommandType);
