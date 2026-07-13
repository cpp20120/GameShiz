using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

public abstract class GameRecordWriter<TRecord> : IGameRecordWriter
    where TRecord : class, IGameRecord
{
    public Type RecordType => typeof(TRecord);

    public Task WriteAsync(IGameRecord record, IGameExecutionContext context, CancellationToken ct) =>
        WriteAsync((TRecord)record, context, ct);

    protected abstract Task WriteAsync(TRecord record, IGameExecutionContext context, CancellationToken ct);
}
