using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class AuthorizationPropertyTests
{
    private static readonly ChatMemberRole[] Roles =
    [
        ChatMemberRole.Member,
        ChatMemberRole.Helper,
        ChatMemberRole.Moderator,
        ChatMemberRole.Admin,
        ChatMemberRole.Owner,
        ChatMemberRole.Restricted,
    ];

    private static readonly Permission[] Permissions = Enum.GetValues<Permission>();

    [Property(MaxTest = 500)]
    public Property ALowerOrEqualRoleNeverModeratesAHigherRole(
        NonNegativeInt actorSeed,
        NonNegativeInt targetSeed,
        NonNegativeInt permissionSeed)
    {
        var actorRole = Roles[actorSeed.Get % Roles.Length];
        var targetRole = Roles[targetSeed.Get % Roles.Length];
        var permission = Permissions[permissionSeed.Get % Permissions.Length];
        var decision = AuthorizationPolicy.Authorize(Member(actorRole, 1), Member(targetRole, 2), permission);
        var actorRank = AuthorizationPolicy.Rank(actorRole);
        var targetRank = AuthorizationPolicy.Rank(targetRole);

        return (targetRank < actorRank || !decision.Allowed)
            .ToProperty()
            .Label($"actor={actorRole}, target={targetRole}, permission={permission}");
    }

    [Property(MaxTest = 500)]
    public Property OwnerIsNeverAValidModerationTarget(NonNegativeInt actorSeed, NonNegativeInt permissionSeed)
    {
        var actorRole = Roles[actorSeed.Get % Roles.Length];
        var permission = Permissions[permissionSeed.Get % Permissions.Length];
        var decision = AuthorizationPolicy.Authorize(Member(actorRole, 1), Member(ChatMemberRole.Owner, 2), permission);

        return (!decision.Allowed
                && (actorRole != ChatMemberRole.Owner || decision.ErrorCode == "owner_protected"))
            .ToProperty()
            .Label($"actor={actorRole}, permission={permission}, error={decision.ErrorCode}");
    }

    [Property(MaxTest = 500)]
    public Property ElevatedRolesCanUseEveryPermissionAgainstMembers(
        NonNegativeInt roleSeed,
        NonNegativeInt permissionSeed)
    {
        var actorRole = roleSeed.Get % 2 == 0 ? ChatMemberRole.Admin : ChatMemberRole.Owner;
        var permission = Permissions[permissionSeed.Get % Permissions.Length];
        var decision = AuthorizationPolicy.Authorize(Member(actorRole, 1), Member(ChatMemberRole.Member, 2), permission);

        return decision.Allowed
            .ToProperty()
            .Label($"actor={actorRole}, permission={permission}, error={decision.ErrorCode}");
    }

    private static MemberState Member(ChatMemberRole role, long userId) => new()
    {
        ChatId = new ChatId(-100),
        UserId = new UserId(userId),
        DisplayName = "user",
        Roles = new HashSet<ChatMemberRole> { role },
    };
}
