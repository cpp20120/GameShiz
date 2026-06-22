namespace BotFramework.Host.Events.Dispatch;

public sealed record EventDispatchRetryResult(
    bool Success,
    bool NotFound,
    string? Message = null);
