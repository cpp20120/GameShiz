using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Authorization;

public static class AuthorizationPolicy
{
    public static AuthorizationDecision Authorize(
        MemberState actor,
        MemberState target,
        Permission permission)
        => Authorize(null, actor, target, permission);

    public static AuthorizationDecision Authorize(
        ChatState? chat,
        MemberState actor,
        MemberState target,
        Permission permission)
    {
        var actorRank = EffectiveRank(chat, actor);
        var targetRank = EffectiveRank(chat, target);

        if (!HasPermission(chat, actor, permission))
            return AuthorizationDecision.Deny("permission_denied");

        if (target.Roles.Contains(ChatMemberRole.Owner))
            return AuthorizationDecision.Deny("owner_protected");

        return targetRank >= actorRank ? AuthorizationDecision.Deny("target_role_too_high") : AuthorizationDecision.Allow();
    }

    public static ChatMemberRole HighestRole(IReadOnlySet<ChatMemberRole> roles)
    {
        if (roles.Contains(ChatMemberRole.Owner)) return ChatMemberRole.Owner;
        if (roles.Contains(ChatMemberRole.Admin)) return ChatMemberRole.Admin;
        if (roles.Contains(ChatMemberRole.Moderator)) return ChatMemberRole.Moderator;
        if (roles.Contains(ChatMemberRole.Helper)) return ChatMemberRole.Helper;
        if (roles.Contains(ChatMemberRole.Trusted)) return ChatMemberRole.Trusted;
        return roles.Contains(ChatMemberRole.Restricted) ? ChatMemberRole.Restricted : ChatMemberRole.Member;
    }

    public static bool HasPermission(ChatMemberRole role, Permission permission) => role switch
    {
        ChatMemberRole.Owner or ChatMemberRole.Admin => true,
        ChatMemberRole.Moderator => permission is Permission.MembersMute or Permission.MembersUnmute
            or Permission.MembersWarn or Permission.MembersRemoveWarning
            or Permission.MembersViewWarnings or Permission.MembersView
            or Permission.MessagesDelete or Permission.CasesView,
        ChatMemberRole.Helper => permission is Permission.MembersWarn
            or Permission.MembersViewWarnings or Permission.MembersView,
        _ => false,
    };

    public static bool HasPermission(ChatState? chat, MemberState member, Permission permission)
    {
        if (member.ExplicitPermissions.Contains(permission))
            return true;

        if (chat?.Settings.CustomRoles
            .Any(role => member.CustomRoleIds.Contains(role.Id) && role.Permissions.Contains(permission)) == true
)
            return true;

        return HasPermission(HighestRole(member.Roles), permission);
    }

    public static int EffectiveRank(ChatState? chat, MemberState member)
    {
        var builtInRank = Rank(HighestRole(member.Roles));
        var customRank = chat?.Settings.CustomRoles
            .Where(role => member.CustomRoleIds.Contains(role.Id))
            .Select(role => role.Rank)
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        return Math.Max(builtInRank, customRank);
    }

    public static int Rank(ChatMemberRole role) => role switch
    {
        ChatMemberRole.Owner => 100,
        ChatMemberRole.Admin => 80,
        ChatMemberRole.Moderator => 60,
        ChatMemberRole.Helper => 40,
        ChatMemberRole.Trusted => 20,
        ChatMemberRole.Restricted => 5,
        _ => 10,
    };
}
