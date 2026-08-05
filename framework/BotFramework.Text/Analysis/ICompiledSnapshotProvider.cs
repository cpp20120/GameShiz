namespace BotFramework.Text;

/// <summary>
/// Consumer-owned snapshot source. The framework defines publication semantics but not storage or invalidation.
/// </summary>
public interface ICompiledSnapshotProvider<in TKey, TValue>
{
    ValueTask<CompiledSnapshot<TValue>> GetAsync(
        TKey key,
        CancellationToken cancellationToken = default);
}
