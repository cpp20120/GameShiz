using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Commands;

public sealed record ManualModerationCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    string CausationId,
    ChatId ChatId,
    UserId ActorUserId,
    UserId TargetUserId,
    ModerationAction Action,
    TimeSpan? Duration,
    string? Reason,
    DateTimeOffset CreatedAt,
    int? SourceMessageId,
    ChatMemberRole ActorObservedRole,
    ChatMemberRole TargetObservedRole,
    string ActorDisplayName,
    string TargetDisplayName);
