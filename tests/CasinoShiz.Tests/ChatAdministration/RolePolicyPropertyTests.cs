using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class RolePolicyPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly ChatMemberRole[] Roles = Enum.GetValues<ChatMemberRole>();

    [Property(MaxTest = 500)]
    public Property LowerRoleCanNeverChangeHigherTarget(
        NonNegativeInt actorSeed,
        NonNegativeInt targetSeed,
        NonNegativeInt roleSeed)
    {
        var actorRole = Roles[actorSeed.Get % Roles.Length];
        var targetRole = Roles[targetSeed.Get % Roles.Length];
        var role = Roles[roleSeed.Get % Roles.Length];
        var decision = RolePolicy.Change(Chat(), Member(10, actorRole), Member(20, targetRole), role, assign: true);
        var actorRank = AuthorizationPolicy.Rank(actorRole);
        var targetRank = AuthorizationPolicy.Rank(targetRole);

        return (targetRank >= actorRank ? !decision.Accepted : true)
            .ToProperty()
            .Label($"actor={actorRole}, target={targetRole}, role={role}");
    }

    [Fact]
    public void AssignAndRemoveCreateEventsAndUpdateMember()
    {
        var actor = Member(10, ChatMemberRole.Admin);
        var target = Member(20, ChatMemberRole.Member);
        var assigned = RolePolicy.Change(Chat(), actor, target, ChatMemberRole.Moderator, true);
        Assert.True(assigned.Accepted);
        Assert.Contains(ChatMemberRole.Moderator, assigned.Member!.Roles);
        Assert.IsType<MemberRoleAssigned>(Assert.Single(assigned.Events));

        var removed = RolePolicy.Change(Chat(), actor, assigned.Member, ChatMemberRole.Moderator, false);
        Assert.True(removed.Accepted);
        Assert.DoesNotContain(ChatMemberRole.Moderator, removed.Member!.Roles);
        Assert.IsType<MemberRoleRemoved>(Assert.Single(removed.Events));
    }

    [Fact]
    public void PolicyRejectsEveryInvalidRoleMutation()
    {
        var admin = Member(10, ChatMemberRole.Admin);
        var target = Member(20, ChatMemberRole.Member);
        Assert.Equal("chat_disabled", RolePolicy.Change(Chat(false), admin, target, ChatMemberRole.Moderator, true).ErrorCode);
        Assert.Equal("permission_denied", RolePolicy.Change(Chat(), Member(10, ChatMemberRole.Member), target, ChatMemberRole.Moderator, true).ErrorCode);
        Assert.Equal("owner_protected", RolePolicy.Change(Chat(), admin, Member(20, ChatMemberRole.Owner), ChatMemberRole.Moderator, true).ErrorCode);
        Assert.Equal("target_role_too_high", RolePolicy.Change(Chat(), admin, Member(20, ChatMemberRole.Admin), ChatMemberRole.Helper, true).ErrorCode);
        Assert.Equal("role_too_high", RolePolicy.Change(Chat(), admin, target, ChatMemberRole.Admin, true).ErrorCode);
        Assert.Equal("role_not_assignable", RolePolicy.Change(Chat(), admin, target, ChatMemberRole.Member, true).ErrorCode);
        Assert.Equal("role_not_assignable", RolePolicy.Change(Chat(), admin, target, ChatMemberRole.Restricted, true).ErrorCode);
        Assert.Equal("role_already_assigned", RolePolicy.Change(Chat(), admin, Member(20, ChatMemberRole.Member, ChatMemberRole.Helper), ChatMemberRole.Helper, true).ErrorCode);
        Assert.Equal("role_not_assigned", RolePolicy.Change(Chat(), admin, target, ChatMemberRole.Helper, false).ErrorCode);
    }

    [Property(MaxTest = 500)]
    public Property CustomRolePermissionRespectsEffectiveRank(PositiveInt actorSeed, PositiveInt targetSeed)
    {
        var actorRank = 21 + actorSeed.Get % 79;
        var targetRank = 21 + targetSeed.Get % 79;
        var chat = Chat() with
        {
            Settings = new ChatSettings
            {
                CustomRoles =
                [
                    new CustomRoleDefinition
                    {
                        Id = new RoleId("actor-role"),
                        DisplayName = "Actor",
                        Rank = actorRank,
                        Permissions = new HashSet<Permission> { Permission.MembersMute },
                    },
                    new CustomRoleDefinition
                    {
                        Id = new RoleId("target-role"),
                        DisplayName = "Target",
                        Rank = targetRank,
                    },
                ],
            },
        };
        var actor = Member(10, ChatMemberRole.Member) with
        {
            CustomRoleIds = new HashSet<RoleId> { new("actor-role") },
        };
        var target = Member(20, ChatMemberRole.Member) with
        {
            CustomRoleIds = new HashSet<RoleId> { new("target-role") },
        };
        var decision = AuthorizationPolicy.Authorize(chat, actor, target, Permission.MembersMute);

        return (targetRank < actorRank ? decision.Allowed : !decision.Allowed)
            .ToProperty()
            .Label($"actorRank={actorRank}, targetRank={targetRank}");
    }

    [Fact]
    public void CustomRolePolicyCreatesAndRemovesPersistableDefinition()
    {
        var chat = Chat();
        var actor = Member(10, ChatMemberRole.Admin);
        var created = CustomRolePolicy.Change(
            chat,
            actor,
            new RoleId("support"),
            "Support",
            50,
            new HashSet<Permission> { Permission.MembersWarn },
            remove: false);

        Assert.True(created.Accepted, created.ErrorCode);
        var definition = Assert.Single(created.Settings!.CustomRoles);
        Assert.Equal(new RoleId("support"), definition.Id);
        Assert.Contains(Permission.MembersWarn, definition.Permissions);

        var removed = CustomRolePolicy.Change(
            chat with { Settings = created.Settings },
            actor,
            definition.Id,
            "",
            0,
            new HashSet<Permission>(),
            remove: true);

        Assert.True(removed.Accepted, removed.ErrorCode);
        Assert.Empty(removed.Settings!.CustomRoles);
    }

    [Fact]
    public void CustomRolePolicyRejectsInvalidAndPrivilegedDefinitions()
    {
        var chat = Chat();
        var actor = Member(10, ChatMemberRole.Moderator) with
        {
            ExplicitPermissions = new HashSet<Permission> { Permission.RolesManage },
        };
        Assert.Equal("invalid_role_id", CustomRolePolicy.Change(chat, actor, new RoleId("admin"), "Admin", 20, new HashSet<Permission>(), false).ErrorCode);
        Assert.Equal("invalid_role_id", CustomRolePolicy.Change(chat, actor, new RoleId("bad:id"), "Bad", 20, new HashSet<Permission>(), false).ErrorCode);
        Assert.Equal("invalid_role_name", CustomRolePolicy.Change(chat, actor, new RoleId("helper-role"), "", 20, new HashSet<Permission>(), false).ErrorCode);
        Assert.Equal("invalid_role_rank", CustomRolePolicy.Change(chat, actor, new RoleId("helper-role"), "Helper", 0, new HashSet<Permission>(), false).ErrorCode);
        Assert.Equal("role_too_high", CustomRolePolicy.Change(chat, actor, new RoleId("admin-role"), "Admin", 60, new HashSet<Permission>(), false).ErrorCode);
        Assert.Equal("custom_role_not_found", CustomRolePolicy.Change(chat, actor, new RoleId("missing"), "", 0, new HashSet<Permission>(), true).ErrorCode);
    }

    [Fact]
    public void CustomRolePolicyCoversAccessAndExistingRoleBranches()
    {
        var roleId = new RoleId("support");
        var role = new CustomRoleDefinition
        {
            Id = roleId,
            DisplayName = "Support",
            Rank = 50,
            Permissions = new HashSet<Permission> { Permission.MembersWarn },
        };
        var chat = Chat() with { Settings = new ChatSettings { CustomRoles = [role] } };
        var moderator = Member(10, ChatMemberRole.Moderator) with
        {
            ExplicitPermissions = new HashSet<Permission> { Permission.RolesManage },
        };

        Assert.Equal("chat_disabled", CustomRolePolicy.Change(Chat(false), moderator, roleId, "Support", 20, new HashSet<Permission>(), false).ErrorCode);
        Assert.Equal("permission_denied", CustomRolePolicy.Change(chat, Member(10, ChatMemberRole.Member), roleId, "Support", 20, new HashSet<Permission>(), false).ErrorCode);
        Assert.Equal("invalid_role_id", CustomRolePolicy.Change(chat, moderator, new RoleId(" "), "Support", 20, new HashSet<Permission>(), false).ErrorCode);
        Assert.Equal("role_too_high", CustomRolePolicy.Change(
            chat with { Settings = new ChatSettings { CustomRoles = [role with { Rank = 80 }] } },
            moderator,
            roleId,
            "Support",
            20,
            new HashSet<Permission>(),
            true).ErrorCode);

        var created = CustomRolePolicy.Change(
            chat,
            moderator,
            new RoleId("helper-role"),
            " Helper ",
            40,
            new HashSet<Permission> { Permission.MembersWarn },
            false);
        Assert.True(created.Accepted, created.ErrorCode);
        Assert.Equal(new[] { 50, 40 }, created.Settings!.CustomRoles.Select(item => item.Rank));
    }

    [Fact]
    public void CustomRoleAssignmentSupportsAssignAndRemoveAndRejectsInvalidTargets()
    {
        var roleId = new RoleId("support");
        var chat = Chat() with
        {
            Settings = new ChatSettings
            {
                CustomRoles =
                [
                    new CustomRoleDefinition
                    {
                        Id = roleId,
                        DisplayName = "Support",
                        Rank = 50,
                        Permissions = new HashSet<Permission> { Permission.MembersWarn },
                    },
                ],
            },
        };
        var actor = Member(10, ChatMemberRole.Admin);
        var target = Member(20, ChatMemberRole.Member);

        Assert.Equal("custom_role_not_found", RolePolicy.ChangeCustom(chat, actor, target, new RoleId("missing"), true).ErrorCode);
        Assert.Equal("chat_disabled", RolePolicy.ChangeCustom(chat with { IsEnabled = false }, actor, target, roleId, true).ErrorCode);
        Assert.Equal("permission_denied", RolePolicy.ChangeCustom(chat, Member(10, ChatMemberRole.Member), target, roleId, true).ErrorCode);
        Assert.Equal("owner_protected", RolePolicy.ChangeCustom(chat, actor, Member(20, ChatMemberRole.Owner), roleId, true).ErrorCode);
        Assert.Equal("target_role_too_high", RolePolicy.ChangeCustom(chat, actor, Member(20, ChatMemberRole.Admin), roleId, true).ErrorCode);
        Assert.Equal("role_too_high", RolePolicy.ChangeCustom(
            chat with { Settings = new ChatSettings { CustomRoles = [chat.Settings.CustomRoles.Single() with { Rank = 80 }] } },
            actor,
            target,
            roleId,
            true).ErrorCode);

        var assigned = RolePolicy.ChangeCustom(chat, actor, target, roleId, true);
        Assert.True(assigned.Accepted, assigned.ErrorCode);
        Assert.Equal("role_already_assigned", RolePolicy.ChangeCustom(chat, actor, assigned.Member!, roleId, true).ErrorCode);
        var removed = RolePolicy.ChangeCustom(chat, actor, assigned.Member!, roleId, false);
        Assert.True(removed.Accepted, removed.ErrorCode);
        Assert.Equal("role_not_assigned", RolePolicy.ChangeCustom(chat, actor, target, roleId, false).ErrorCode);
    }

    [Fact]
    public void RoleIdHasStableStringRepresentation()
    {
        Assert.Equal("custom", new RoleId("custom").ToString());
    }

    private static ChatState Chat(bool enabled = true) => new()
    {
        Id = new ChatId(-100),
        Type = ChatType.Supergroup,
        Title = "chat",
        IsEnabled = enabled,
        CreatedAt = Now,
        UpdatedAt = Now,
    };

    private static MemberState Member(long id, ChatMemberRole role, params ChatMemberRole[] extraRoles) => new()
    {
        ChatId = new ChatId(-100),
        UserId = new UserId(id),
        DisplayName = "member",
        Roles = new HashSet<ChatMemberRole>(new[] { role }.Concat(extraRoles)),
    };
}
