namespace BotFramework.Host.Contracts.Economics;

public interface IDistributedGameLock
{
    Task<IAsyncDisposable> AcquireAsync(string resource, CancellationToken ct);
}
