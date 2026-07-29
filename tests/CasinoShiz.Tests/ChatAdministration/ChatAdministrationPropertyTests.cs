using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class ChatAdministrationPropertyTests
{
    [Property(MaxTest = 200)]
    public Property LowerRoleNeverModeratesHigherRole(NonNegativeInt actorSeed, NonNegativeInt targetSeed)
    {
        var roles = new[]
        {
            ChatMemberRole.Member,
            ChatMemberRole.Helper,
            ChatMemberRole.Moderator,
            ChatMemberRole.Admin,
            ChatMemberRole.Owner,
        };
        var actorRole = roles[actorSeed.Get % roles.Length];
        var targetRole = roles[targetSeed.Get % roles.Length];
        var actor = Member(actorRole, 1);
        var target = Member(targetRole, 2);
        var decision = AuthorizationPolicy.Authorize(actor, target, Permission.MembersMute);
        var actorRank = AuthorizationPolicy.EffectiveRank(null, actor);
        var targetRank = AuthorizationPolicy.EffectiveRank(null, target);

        return (targetRank < actorRank || !decision.Allowed).ToProperty();
    }

    private static MemberState Member(ChatMemberRole role, long userId) => new()
    {
        ChatId = new ChatId(-100),
        UserId = new UserId(userId),
        DisplayName = "user",
        Roles = new HashSet<ChatMemberRole> { role },
    };
}
