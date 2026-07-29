namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record CaseRow(
    Guid CaseId,
    long ChatId,
    long TargetUserId,
    long? ActorUserId,
    string ActorType,
    string Action,
    string? Reason,
    int? SourceMessageId,
    string? SourceRuleId,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    string Status,
    string CorrelationId);
