using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public static class RolePolicy
{
    public static RoleDecision ChangeCustom(
        ChatState chat,
        MemberState actor,
        MemberState target,
        RoleId roleId,
        bool assign)
    {
        var definition = chat.Settings.CustomRoles.FirstOrDefault(role => role.Id == roleId);
        if (definition is null)
            return RoleDecision.Reject("custom_role_not_found");
        if (!chat.IsEnabled)
            return RoleDecision.Reject("chat_disabled");
        if (!AuthorizationPolicy.HasPermission(chat, actor, Permission.RolesManage))
            return RoleDecision.Reject("permission_denied");
        if (target.Roles.Contains(ChatMemberRole.Owner))
            return RoleDecision.Reject("owner_protected");

        var actorRank = AuthorizationPolicy.EffectiveRank(chat, actor);
        var targetRank = AuthorizationPolicy.EffectiveRank(chat, target);
        if (targetRank >= actorRank)
            return RoleDecision.Reject("target_role_too_high");
        if (definition.Rank >= actorRank)
            return RoleDecision.Reject("role_too_high");
        if (assign && target.CustomRoleIds.Contains(roleId))
            return RoleDecision.Reject("role_already_assigned");
        if (!assign && !target.CustomRoleIds.Contains(roleId))
            return RoleDecision.Reject("role_not_assigned");

        var roles = target.CustomRoleIds.ToHashSet();
        if (assign)
            roles.Add(roleId);
        else
            roles.Remove(roleId);
        var member = target with { CustomRoleIds = roles };
        var domainEvent = assign
            ? (IDomainEvent)new CustomRoleAssigned(target.ChatId, target.UserId, roleId)
            : new CustomRoleRemoved(target.ChatId, target.UserId, roleId);
        return new RoleDecision(true, null, member, [domainEvent]);
    }

    public static RoleDecision Change(
        ChatState chat,
        MemberState actor,
        MemberState target,
        ChatMemberRole role,
        bool assign)
    {
        if (!chat.IsEnabled)
            return RoleDecision.Reject("chat_disabled");
        if (!AuthorizationPolicy.HasPermission(chat, actor, Permission.RolesManage))
            return RoleDecision.Reject("permission_denied");
        if (target.Roles.Contains(ChatMemberRole.Owner))
            return RoleDecision.Reject("owner_protected");

        var actorRank = AuthorizationPolicy.EffectiveRank(chat, actor);
        var targetRank = AuthorizationPolicy.EffectiveRank(chat, target);
        if (targetRank >= actorRank)
            return RoleDecision.Reject("target_role_too_high");
        if (role == ChatMemberRole.Member || role == ChatMemberRole.Restricted)
            return RoleDecision.Reject("role_not_assignable");
        if (AuthorizationPolicy.Rank(role) >= actorRank)
            return RoleDecision.Reject("role_too_high");
        if (assign && target.Roles.Contains(role))
            return RoleDecision.Reject("role_already_assigned");
        if (!assign && !target.Roles.Contains(role))
            return RoleDecision.Reject("role_not_assigned");

        var roles = target.Roles.ToHashSet();
        if (assign)
            roles.Add(role);
        else
            roles.Remove(role);
        var member = target with { Roles = roles };
        var domainEvent = assign
            ? (IDomainEvent)new MemberRoleAssigned(target.ChatId, target.UserId, role)
            : new MemberRoleRemoved(target.ChatId, target.UserId, role);
        return new RoleDecision(true, null, member, [domainEvent]);
    }
}
