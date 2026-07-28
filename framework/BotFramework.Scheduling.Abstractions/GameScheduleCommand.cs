namespace BotFramework.Scheduling.Abstractions;

public sealed record GameScheduleCommand(
    string ScheduleId,
    string JobKey,
    ScheduleDescriptor Schedule,
    IReadOnlyDictionary<string, string>? Data = null);
