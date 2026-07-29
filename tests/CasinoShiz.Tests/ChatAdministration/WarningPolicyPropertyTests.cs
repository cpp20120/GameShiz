using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class WarningPolicyPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly ChatMemberRole[] Roles = Enum.GetValues<ChatMemberRole>();

    [Property(MaxTest = 500)]
    public Property RevocationRequiresCentralizedPermission(
        NonNegativeInt actorSeed,
        NonNegativeInt targetSeed)
    {
        var actorRole = Roles[actorSeed.Get % Roles.Length];
        var targetRole = Roles[targetSeed.Get % Roles.Length];
        var actor = Member(10, actorRole);
        var target = Member(20, targetRole);
        var warning = Warning();
        var decision = WarningPolicy.Revoke(Chat(), actor, target, warning, WarningRevocationReason.Manual);
        var expected = AuthorizationPolicy.Authorize(actor, target, Permission.MembersRemoveWarning).Allowed;

        return (decision.Accepted == expected).ToProperty()
            .Label($"actor={actorRole}, target={targetRole}, error={decision.ErrorCode}");
    }

    [Fact]
    public void AcceptedRevocationDeactivatesWarningAndRecordsReason()
    {
        var decision = WarningPolicy.Revoke(
            Chat(),
            Member(10, ChatMemberRole.Admin),
            Member(20, ChatMemberRole.Member),
            Warning(),
            WarningRevocationReason.Cleared);

        Assert.True(decision.Accepted);
        Assert.Equal(WarningRevocationReason.Cleared, decision.Warning!.RevocationReason);
        Assert.False(decision.Warning.IsActive);
        var revoked = Assert.IsType<WarningRevoked>(Assert.Single(decision.Events));
        Assert.Equal(WarningRevocationReason.Cleared, revoked.Reason);
    }

    [Fact]
    public void RevokeFailsClosedForDisabledChatMismatchedAndInactiveWarnings()
    {
        var actor = Member(10, ChatMemberRole.Admin);
        var target = Member(20, ChatMemberRole.Member);
        Assert.Equal("moderation_disabled", WarningPolicy.Revoke(Chat(false), actor, target, Warning(), WarningRevocationReason.Manual).ErrorCode);
        Assert.Equal("warning_target_mismatch", WarningPolicy.Revoke(Chat() with { Id = new ChatId(-200) }, actor, target, Warning(), WarningRevocationReason.Manual).ErrorCode);
        Assert.Equal("warning_target_mismatch", WarningPolicy.Revoke(Chat(), actor, target, Warning() with { TargetUserId = new UserId(99) }, WarningRevocationReason.Manual).ErrorCode);
        Assert.Equal("warning_not_active", WarningPolicy.Revoke(Chat(), actor, target, Warning() with { IsActive = false }, WarningRevocationReason.Manual).ErrorCode);
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

    private static MemberState Member(long id, ChatMemberRole role) => new()
    {
        ChatId = new ChatId(-100),
        UserId = new UserId(id),
        DisplayName = "member",
        Roles = new HashSet<ChatMemberRole> { role },
    };

    private static WarningState Warning() => new()
    {
        Id = WarningId.New(),
        ChatId = new ChatId(-100),
        TargetUserId = new UserId(20),
        CreatedAt = Now,
        IsActive = true,
    };
}
