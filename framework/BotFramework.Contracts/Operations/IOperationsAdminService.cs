namespace BotFramework.Contracts.Operations;

public sealed record OperationFailure(long Id, string StreamId, long StreamVersion, string EventType,
    string Stage, string HandlerName, string Error, string? ErrorType, int RetryCount,
    DateTimeOffset CreatedAt, DateTimeOffset LastSeenAt);
public sealed record OperationOutbox(long Id, long ChatId, string Status, int Attempts,
    DateTimeOffset NextAttemptAt, DateTimeOffset? LockedUntil, string? LastError, string MessagePreview,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record OperationJob(string Name, string Kind, string State, DateTimeOffset? LastStartedAt,
    DateTimeOffset? LastHeartbeatAt, DateTimeOffset? LastCompletedAt, DateTimeOffset? LastFailedAt,
    DateTimeOffset? NextRunAt, int CrashCount, int? RestartBackoffMs, string? LastError, string? Note);
public sealed record OperationAudit(long Id, long ActorId, string ActorName, string Action,
    string DetailsJson, DateTimeOffset OccurredAt);
public sealed record OperationMutationResult(bool Success, string Message);

public interface IOperationsAdminService
{
    Task<IReadOnlyList<OperationFailure>> ListFailuresAsync(int limit, string? eventType, CancellationToken ct);
    Task<IReadOnlyList<OperationOutbox>> ListOutboxAsync(int limit, string? status, CancellationToken ct);
    Task<IReadOnlyList<OperationJob>> ListJobsAsync(CancellationToken ct);
    Task<IReadOnlyList<OperationAudit>> ListAuditAsync(int limit, string? actor, string? action,
        string? details, DateTimeOffset? from, DateTimeOffset? until, CancellationToken ct);
    Task<OperationMutationResult> RetryEventAsync(long id, long actorId, string actorName, CancellationToken ct);
    Task<OperationMutationResult> RescheduleOutboxAsync(long id, long actorId, string actorName, CancellationToken ct);
    Task<OperationMutationResult> AdjustWalletAsync(long userId, long balanceScopeId, int delta,
        string operationId, long actorId, string actorName, CancellationToken ct);
}
