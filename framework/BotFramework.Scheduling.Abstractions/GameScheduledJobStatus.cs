namespace BotFramework.Scheduling.Abstractions;

public sealed record GameScheduledJobStatus(
    string ScheduleId,
    string JobKey,
    string State,
    DateTimeOffset? PreviousFireTime,
    DateTimeOffset? NextFireTime);
