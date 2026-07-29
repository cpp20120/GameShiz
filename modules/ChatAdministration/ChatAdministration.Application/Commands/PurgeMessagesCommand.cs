using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Commands;

public sealed record PurgeMessagesCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    string CausationId,
    ChatId ChatId,
    UserId ActorUserId,
    UserId? TargetUserId,
    int Count,
    int SourceMessageId,
    DateTimeOffset CreatedAt,
    ChatMemberRole ActorObservedRole);
