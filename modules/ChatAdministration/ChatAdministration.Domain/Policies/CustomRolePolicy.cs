using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public static class CustomRolePolicy
{
    public static CustomRoleDecision Change(
        ChatState chat,
        MemberState actor,
        RoleId roleId,
        string displayName,
        int rank,
        IReadOnlySet<Permission> permissions,
        bool remove)
    {
        if (!chat.IsEnabled)
            return CustomRoleDecision.Reject("chat_disabled");
        if (!AuthorizationPolicy.HasPermission(chat, actor, Permission.RolesManage))
            return CustomRoleDecision.Reject("permission_denied");
        if (string.IsNullOrWhiteSpace(roleId.Value) || roleId.Value.Length > 64)
            return CustomRoleDecision.Reject("invalid_role_id");
        if (roleId.Value.Contains(':'))
            return CustomRoleDecision.Reject("invalid_role_id");
        if (Enum.TryParse<ChatMemberRole>(roleId.Value, true, out _))
            return CustomRoleDecision.Reject("invalid_role_id");

        var existing = chat.Settings.CustomRoles.FirstOrDefault(role => role.Id == roleId);
        if (remove)
        {
            if (existing is null)
                return CustomRoleDecision.Reject("custom_role_not_found");
            if (existing.Rank >= AuthorizationPolicy.EffectiveRank(chat, actor))
                return CustomRoleDecision.Reject("role_too_high");

            return new CustomRoleDecision(
                true,
                null,
                chat.Settings with
                {
                    CustomRoles = chat.Settings.CustomRoles.Where(role => role.Id != roleId).ToArray(),
                });
        }

        if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 80)
            return CustomRoleDecision.Reject("invalid_role_name");
        if (rank < 1 || rank >= AuthorizationPolicy.Rank(ChatMemberRole.Owner))
            return CustomRoleDecision.Reject("invalid_role_rank");
        if (rank >= AuthorizationPolicy.EffectiveRank(chat, actor))
            return CustomRoleDecision.Reject("role_too_high");

        var definition = new CustomRoleDefinition
        {
            Id = roleId,
            DisplayName = displayName.Trim(),
            Rank = rank,
            Permissions = permissions,
        };
        var roles = chat.Settings.CustomRoles
            .Where(role => role.Id != roleId)
            .Append(definition)
            .OrderByDescending(role => role.Rank)
            .ToArray();
        return new CustomRoleDecision(true, null, chat.Settings with { CustomRoles = roles });
    }
}
