using System.Data.Common;

namespace BotFramework.Host.Execution;

internal interface IGameExecutionSession : IAsyncDisposable
{
    DbConnection Connection { get; }

    DbTransaction Transaction { get; }

    Task AcquireLocksAsync(IEnumerable<string> lockKeys, CancellationToken ct);

    Task CommitAsync(CancellationToken ct);

    Task RollbackAsync(CancellationToken ct);
}
