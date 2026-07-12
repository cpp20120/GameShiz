using BotFramework.Sdk.Execution;

namespace BotFramework.Host.Execution;

public interface IGameRecordWriter
{
    Type RecordType { get; }

    Task WriteAsync(IGameRecord record, IGameExecutionContext context, CancellationToken ct);
}
