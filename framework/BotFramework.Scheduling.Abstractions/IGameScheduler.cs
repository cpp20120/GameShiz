namespace BotFramework.Scheduling.Abstractions;

public interface IGameScheduler
{
    Task ScheduleAsync(GameScheduleCommand command, CancellationToken ct);
    Task TriggerNowAsync(string jobKey, IReadOnlyDictionary<string, string> data, CancellationToken ct);
    Task UnscheduleAsync(string scheduleId, CancellationToken ct);
}

public interface IScheduledCommand
{
    string Key { get; }
    Task ExecuteAsync(IReadOnlyDictionary<string, string> data, CancellationToken ct);
}

/// <summary>A command whose cadence is declared by its owning module.</summary>
public interface IRecurringScheduledCommand : IScheduledCommand
{
    ScheduleDescriptor Schedule { get; }
}

public sealed record GameScheduleCommand(
    string ScheduleId,
    string JobKey,
    ScheduleDescriptor Schedule,
    IReadOnlyDictionary<string, string>? Data = null);

public sealed record ScheduleDescriptor(
    string? CronExpression = null,
    TimeSpan? RepeatInterval = null,
    string? TimeZoneId = null)
{
    public static ScheduleDescriptor Every(TimeSpan interval) => new(RepeatInterval: interval);
}
