namespace BotFramework.Scheduling.Abstractions;

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
