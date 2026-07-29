using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Commands;

public sealed record CustomRoleMutationCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    ChatId ChatId,
    UserId ActorUserId,
    RoleId RoleId,
    string DisplayName,
    int Rank,
    IReadOnlySet<Permission> Permissions,
    bool Remove,
    ChatMemberRole ActorObservedRole,
    string ActorDisplayName,
    DateTimeOffset CreatedAt,
    int? SourceMessageId);
