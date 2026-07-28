namespace BotFramework.Scheduling.Abstractions;

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
