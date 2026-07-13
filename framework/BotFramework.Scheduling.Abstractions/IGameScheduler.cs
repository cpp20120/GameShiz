namespace BotFramework.Scheduling.Abstractions;

public interface IGameScheduler
{
    Task ScheduleAsync(GameScheduleCommand command, CancellationToken ct);
    Task TriggerNowAsync(string jobKey, IReadOnlyDictionary<string, string> data, CancellationToken ct);
    Task UnscheduleAsync(string scheduleId, CancellationToken ct);
}

/// <summary>Read-only operational view of persistent scheduler state.</summary>
public interface IGameSchedulerStatusReader
{
    Task<IReadOnlyList<GameScheduledJobStatus>> SnapshotAsync(CancellationToken ct);
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
    string? TimeZoneId = null,
    DateTimeOffset? RunAt = null,
    ScheduleExecutionPolicy? Policy = null)
{
    public static ScheduleDescriptor Every(TimeSpan interval) => new(RepeatInterval: interval);

    public static ScheduleDescriptor Once(DateTimeOffset runAt) => new(RunAt: runAt);

    public ScheduleExecutionPolicy EffectivePolicy => Policy ?? ScheduleExecutionPolicy.Default;
}

/// <summary>
/// Runtime semantics shared by Quartz and non-Quartz background runners.
/// The scheduler owns triggering; the effect executor owns transactionality and
/// idempotency. Batch/retry values are metadata so a job can make the same
/// decision regardless of which scheduler implementation invoked it.
/// </summary>
public sealed record ScheduleExecutionPolicy(
    ScheduleMisfirePolicy Misfire = ScheduleMisfirePolicy.FireOnce,
    ScheduleConcurrencyPolicy Concurrency = ScheduleConcurrencyPolicy.Disallow,
    int BatchSize = 1,
    int MaxAttempts = 3,
    TimeSpan? RetryBackoff = null)
{
    public static ScheduleExecutionPolicy Default => new();

    public TimeSpan EffectiveRetryBackoff => RetryBackoff.GetValueOrDefault(TimeSpan.FromSeconds(5));
}

public enum ScheduleMisfirePolicy
{
    FireOnce = 0,
    Ignore = 1,
    DoNothing = 2,
}

public enum ScheduleConcurrencyPolicy
{
    Disallow = 0,
    Allow = 1,
}

public sealed record GameScheduledJobStatus(
    string ScheduleId,
    string JobKey,
    string State,
    DateTimeOffset? PreviousFireTime,
    DateTimeOffset? NextFireTime);
