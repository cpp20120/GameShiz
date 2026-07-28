namespace BotFramework.Scheduling.Abstractions;

/// <summary>Read-only operational view of persistent scheduler state.</summary>
public interface IGameSchedulerStatusReader
{
    Task<IReadOnlyList<GameScheduledJobStatus>> SnapshotAsync(CancellationToken ct);
}
