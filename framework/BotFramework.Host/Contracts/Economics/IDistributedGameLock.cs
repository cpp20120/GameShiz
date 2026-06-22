namespace BotFramework.Host;

public interface IDistributedGameLock
{
    Task<IAsyncDisposable> AcquireAsync(string resource, CancellationToken ct);
}
