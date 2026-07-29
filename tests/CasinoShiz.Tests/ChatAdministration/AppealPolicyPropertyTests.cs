using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Xunit;

namespace CasinoShiz.Tests.ChatAdministration;

public sealed class AppealPolicyPropertyTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly ChatMemberRole[] Roles = Enum.GetValues<ChatMemberRole>();

    [Property(MaxTest = 500)]
    public Property ResolutionRequiresCentralizedAppealsPermission(
        NonNegativeInt actorSeed,
        NonNegativeInt targetSeed,
        NonNegativeInt approvalSeed)
    {
        var actorRole = Roles[actorSeed.Get % Roles.Length];
        var targetRole = Roles[targetSeed.Get % Roles.Length];
        var actor = Member(10, actorRole);
        var target = Member(20, targetRole);
        var moderationCase = Case();
        var appeal = OpenAppeal(moderationCase.Id, target.UserId);
        var decision = AppealPolicy.Resolve(
            Chat(), actor, target, moderationCase, appeal, approvalSeed.Get % 2 == 0, "comment", Now);
        var expected = AuthorizationPolicy.Authorize(actor, target, Permission.AppealsResolve).Allowed;

        return (decision.Accepted == expected).ToProperty()
            .Label($"actor={actorRole}, target={targetRole}, error={decision.ErrorCode}");
    }

    [Fact]
    public void OpenAndResolvePreserveAppealOwnershipAndTransitions()
    {
        var moderationCase = Case();
        var opened = AppealPolicy.Open(Chat(), moderationCase, new UserId(20), "  please review  ", Now);
        Assert.True(opened.Accepted);
        Assert.Equal("please review", opened.Appeal!.Text);
        Assert.Equal(AppealStatus.Open, opened.Appeal.Status);
        Assert.IsType<AppealOpened>(Assert.Single(opened.Events));

        var approved = AppealPolicy.Resolve(
            Chat(), Member(10, ChatMemberRole.Admin), Member(20, ChatMemberRole.Member),
            moderationCase, opened.Appeal, true, "  accepted  ", Now.AddMinutes(1));
        Assert.True(approved.Accepted);
        Assert.Equal(AppealStatus.Approved, approved.Appeal!.Status);
        Assert.Equal("accepted", approved.Appeal.ResolutionComment);
        Assert.IsType<AppealApproved>(Assert.Single(approved.Events));

        var rejected = AppealPolicy.Resolve(
            Chat(), Member(10, ChatMemberRole.Admin), Member(20, ChatMemberRole.Member),
            moderationCase, OpenAppeal(moderationCase.Id, new UserId(20)), false, null, Now);
        Assert.True(rejected.Accepted);
        Assert.Equal(AppealStatus.Rejected, rejected.Appeal!.Status);
        Assert.IsType<AppealRejected>(Assert.Single(rejected.Events));
    }

    [Fact]
    public void PolicyFailsClosedForInvalidOpenAndResolveInputs()
    {
        var moderationCase = Case();
        var target = Member(20, ChatMemberRole.Member);
        var appeal = OpenAppeal(moderationCase.Id, target.UserId);
        Assert.Equal("chat_disabled", AppealPolicy.Open(Chat(false), moderationCase, target.UserId, "text", Now).ErrorCode);
        Assert.Equal("chat_disabled", AppealPolicy.Resolve(Chat(false), Member(10, ChatMemberRole.Admin), target, moderationCase, appeal, true, null, Now).ErrorCode);
        Assert.Equal("case_chat_mismatch", AppealPolicy.Open(Chat(), moderationCase with { ChatId = new ChatId(-200) }, target.UserId, "text", Now).ErrorCode);
        Assert.Equal("appeal_author_mismatch", AppealPolicy.Open(Chat(), moderationCase, new UserId(99), "text", Now).ErrorCode);
        Assert.Equal("case_action_not_appealable", AppealPolicy.Open(Chat(), moderationCase with { Action = ModerationAction.Warn }, target.UserId, "text", Now).ErrorCode);
        Assert.Equal("case_not_appealable", AppealPolicy.Open(Chat(), moderationCase with { Status = ModerationCaseStatus.Revoked }, target.UserId, "text", Now).ErrorCode);
        Assert.Equal("invalid_appeal_text", AppealPolicy.Open(Chat(), moderationCase, target.UserId, " ", Now).ErrorCode);
        Assert.Equal("invalid_appeal_text", AppealPolicy.Open(Chat(), moderationCase, target.UserId, new string('x', 2001), Now).ErrorCode);
        Assert.Equal("appeal_case_mismatch", AppealPolicy.Resolve(Chat(), Member(10, ChatMemberRole.Admin), target, moderationCase with { Id = ModerationCaseId.New() }, appeal, true, null, Now).ErrorCode);
        Assert.Equal("appeal_not_open", AppealPolicy.Resolve(Chat(), Member(10, ChatMemberRole.Admin), target, moderationCase, appeal with { Status = AppealStatus.Rejected }, true, null, Now).ErrorCode);
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

    private static ModerationCaseState Case() => new()
    {
        Id = ModerationCaseId.New(),
        ChatId = new ChatId(-100),
        TargetUserId = new UserId(20),
        Action = ModerationAction.Ban,
        CreatedAt = Now,
        Status = ModerationCaseStatus.Applied,
        CorrelationId = "case-correlation",
    };

    private static AppealState OpenAppeal(ModerationCaseId caseId, UserId authorUserId) => new()
    {
        Id = AppealId.New(),
        CaseId = caseId,
        AuthorUserId = authorUserId,
        Text = "review",
        Status = AppealStatus.Open,
        CreatedAt = Now,
    };
}
