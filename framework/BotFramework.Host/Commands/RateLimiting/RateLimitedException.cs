using System.Globalization;

namespace BotFramework.Host.Commands.RateLimiting;

public sealed class RateLimitedException : Exception
{
    public RateLimitedException()
    {
    }

    public RateLimitedException(string? message) : base(message)
    {
    }

    public RateLimitedException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    public RateLimitedException(RateLimitKey key)
        : base(string.Create(CultureInfo.InvariantCulture, $"rate-limited: user={key.UserId} command={key.CommandType}"))
    {
    }
}
