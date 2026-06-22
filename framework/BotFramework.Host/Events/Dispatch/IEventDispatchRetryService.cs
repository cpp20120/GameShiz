namespace BotFramework.Host.Events;

public interface IEventDispatchRetryService
{
    Task<EventDispatchRetryResult> RetryAsync(long failureId, CancellationToken ct);
}
