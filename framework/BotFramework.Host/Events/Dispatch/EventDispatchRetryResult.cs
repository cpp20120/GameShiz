namespace BotFramework.Host.Events;

public sealed record EventDispatchRetryResult(
    bool Success,
    bool NotFound,
    string? Message = null);
