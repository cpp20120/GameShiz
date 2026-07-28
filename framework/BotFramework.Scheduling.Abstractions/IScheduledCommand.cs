namespace BotFramework.Scheduling.Abstractions;

public interface IScheduledCommand
{
    string Key { get; }
    Task ExecuteAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct);
}
