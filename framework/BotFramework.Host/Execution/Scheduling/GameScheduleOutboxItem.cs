namespace BotFramework.Host.Execution;

internal sealed record GameScheduleOutboxItem(
    long Id,
    string EffectKind,
    string ScheduleId,
    string? JobKey,
    long? DueAtUnixMilliseconds,
    string Data,
    int Attempts);
