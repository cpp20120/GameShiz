namespace BotFramework.Sdk.Execution;

public sealed record ScheduleEffect(
    ScheduleEffectKind Kind,
    string ScheduleId,
    string? JobKey = null,
    DateTimeOffset? DueAt = null,
    IReadOnlyDictionary<string, string>? Data = null) : IGameEffect
{
    public static ScheduleEffect Schedule(
        string scheduleId,
        string jobKey,
        DateTimeOffset dueAt,
        IReadOnlyDictionary<string, string>? data = null) =>
        new(ScheduleEffectKind.Schedule, scheduleId, jobKey, dueAt, data);

    public static ScheduleEffect Cancel(string scheduleId) =>
        new(ScheduleEffectKind.Cancel, scheduleId);

    public static ScheduleEffect ScheduleCommand<TCommand>(
        string scheduleId,
        DateTimeOffset dueAt,
        TCommand command) =>
        Schedule(
            scheduleId,
            AtomicGameSchedule.JobKey<TCommand>(),
            dueAt,
            AtomicGameSchedule.SerializeCommand(command));
}
