namespace BotFramework.Host.Runtime.Jobs;

public sealed record BackgroundJobStatusSnapshot(
    string Name,
    string Kind,
    string State,
    DateTimeOffset? LastStartedAt,
    DateTimeOffset? LastHeartbeatAt,
    DateTimeOffset? LastCompletedAt,
    DateTimeOffset? LastFailedAt,
    DateTimeOffset? NextRunAt,
    int CrashCount,
    int? RestartBackoffMs,
    string? LastError,
    string? Note);
