namespace BotFramework.Host.Events;

public interface IEventDispatchFailureStore
{
    Task RecordAsync(EventDispatchFailure failure, CancellationToken ct);
    Task<IReadOnlyList<EventDispatchFailureRow>> ListUnresolvedAsync(int limit, CancellationToken ct);
    Task<EventDispatchFailureRow?> FindAsync(long id, CancellationToken ct);
    Task MarkResolvedAsync(long id, CancellationToken ct);
}
