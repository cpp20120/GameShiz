namespace BotFramework.Host.Events.Dispatch;

public interface IEventDispatchRetryService
{
    Task<EventDispatchRetryResult> RetryAsync(long failureId, CancellationToken ct);
}
