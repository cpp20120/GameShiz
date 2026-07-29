using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class CasePolicyPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly ChatMemberRole[] Roles = Enum.GetValues<ChatMemberRole>();

    [Property(MaxTest = 500)]
    public Property RevocationUsesCentralizedCasesPermission(
        NonNegativeInt actorSeed,
        NonNegativeInt targetSeed)
    {
        var actorRole = Roles[actorSeed.Get % Roles.Length];
        var targetRole = Roles[targetSeed.Get % Roles.Length];
        var actor = Member(10, actorRole);
        var target = Member(20, targetRole);
        var decision = CasePolicy.Revoke(Chat(), actor, target, Case(ModerationAction.Mute), "corr", "cause");
        var expected = AuthorizationPolicy.Authorize(actor, target, Permission.CasesResolve).Allowed;

        return (decision.Accepted == expected).ToProperty()
            .Label($"actor={actorRole}, target={targetRole}, error={decision.ErrorCode}");
    }

    [Fact]
    public void SupportedMuteAndBanCasesCreateCompensationDag()
    {
        var actor = Member(10, ChatMemberRole.Admin);
        var target = Member(20, ChatMemberRole.Member);
        var mute = CasePolicy.Revoke(Chat(), actor, target, Case(ModerationAction.Mute), "corr", "cause");
        Assert.True(mute.Accepted);
        Assert.Equal(ModerationCaseStatus.Revoking, mute.Case!.Status);
        Assert.IsType<ModerationCaseRevocationRequested>(Assert.Single(mute.Events));
        Assert.IsType<UnrestrictMemberEffect>(mute.EffectPlan.Effects[0].Effect);
        Assert.IsType<MarkModerationCaseRevokedEffect>(mute.EffectPlan.Effects[1].Effect);
        Assert.Equal(mute.EffectPlan.Effects[0].Id, mute.EffectPlan.Effects[1].DependsOn.Single());

        var ban = CasePolicy.Revoke(Chat(), actor, target, Case(ModerationAction.Ban, expiresAt: null), "corr", "cause");
        Assert.True(ban.Accepted);
        Assert.IsType<UnbanMemberEffect>(ban.EffectPlan.Effects[0].Effect);
    }

    [Fact]
    public void PolicyFailsClosedForInvalidCaseInputs()
    {
        var actor = Member(10, ChatMemberRole.Admin);
        var target = Member(20, ChatMemberRole.Member);
        Assert.Equal("chat_disabled", CasePolicy.Revoke(Chat(false), actor, target, Case(ModerationAction.Mute), "corr", "cause").ErrorCode);
        Assert.Equal("case_target_mismatch", CasePolicy.Revoke(Chat(), actor, target, Case(ModerationAction.Mute) with { ChatId = new ChatId(-200) }, "corr", "cause").ErrorCode);
        Assert.Equal("case_target_mismatch", CasePolicy.Revoke(Chat(), actor, target, Case(ModerationAction.Mute) with { TargetUserId = new UserId(99) }, "corr", "cause").ErrorCode);
        Assert.Equal("case_not_revivable", CasePolicy.Revoke(Chat(), actor, target, Case(ModerationAction.Mute) with { Status = ModerationCaseStatus.Requested }, "corr", "cause").ErrorCode);
        Assert.Equal("case_action_not_revivable", CasePolicy.Revoke(Chat(), actor, target, Case(ModerationAction.Warn), "corr", "cause").ErrorCode);
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

    private static ModerationCaseState Case(ModerationAction action, DateTimeOffset? expiresAt = null) => new()
    {
        Id = ModerationCaseId.New(),
        ChatId = new ChatId(-100),
        TargetUserId = new UserId(20),
        Action = action,
        CreatedAt = Now,
        ExpiresAt = expiresAt ?? Now.AddHours(1),
        Status = ModerationCaseStatus.Applied,
        CorrelationId = "case-correlation",
    };
}
