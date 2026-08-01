using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public sealed record RoleMutationCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    string CausationId,
    ChatId ChatId,
    UserId ActorUserId,
    UserId TargetUserId,
    ChatMemberRole Role,
    bool Assign,
    MemberState ResultMember,
    IDomainEvent Event,
    string ResponseText,
    DateTimeOffset CreatedAt,
    int? SourceMessageId)
{
    public RoleId? CustomRoleId { get; init; }
}
