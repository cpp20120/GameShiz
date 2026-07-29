using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Commands;

public sealed record ChatSettingsCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    ChatId ChatId,
    UserId ActorUserId,
    string? Key,
    string? Value,
    int SourceMessageId,
    DateTimeOffset CreatedAt,
    ChatMemberRole ActorObservedRole,
    string ActorDisplayName);
