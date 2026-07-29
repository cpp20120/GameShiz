using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Commands;

public sealed record MuteMemberCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    string CausationId,
    ChatId ChatId,
    UserId ActorUserId,
    UserId TargetUserId,
    string ActorDisplayName,
    string TargetDisplayName,
    TimeSpan Duration,
    string? Reason,
    DateTimeOffset CreatedAt,
    int? SourceMessageId,
    ChatMemberRole ActorObservedRole,
    ChatMemberRole TargetObservedRole);
