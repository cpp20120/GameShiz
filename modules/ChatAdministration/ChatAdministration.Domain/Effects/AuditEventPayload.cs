using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record AuditEventPayload(
    ChatId ChatId,
    UserId? ActorUserId,
    UserId? TargetUserId,
    string Action,
    string CorrelationId,
    ModerationCaseId? CaseId,
    IReadOnlyDictionary<string, object?> Metadata,
    DateTimeOffset CreatedAt);
