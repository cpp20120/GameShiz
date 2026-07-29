using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record ManualModerationRequest(
    ChatId ChatId,
    UserId ActorUserId,
    UserId TargetUserId,
    ModerationAction Action,
    TimeSpan? Duration,
    string? Reason,
    int? SourceMessageId,
    string CorrelationId,
    string CausationId,
    DateTimeOffset Now);
