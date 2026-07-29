using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public static class AppealPolicy
{
    public static AppealDecision Open(
        ChatState chat,
        ModerationCaseState moderationCase,
        UserId authorUserId,
        string text,
        DateTimeOffset now)
    {
        if (!chat.IsEnabled)
            return AppealDecision.Reject("chat_disabled");
        if (moderationCase.ChatId != chat.Id)
            return AppealDecision.Reject("case_chat_mismatch");
        if (moderationCase.TargetUserId != authorUserId)
            return AppealDecision.Reject("appeal_author_mismatch");
        if (moderationCase.Action is not (ModerationAction.Mute or ModerationAction.Ban))
            return AppealDecision.Reject("case_action_not_appealable");
        if (moderationCase.Status is not (ModerationCaseStatus.Applied
            or ModerationCaseStatus.PartiallyApplied
            or ModerationCaseStatus.Unknown))
            return AppealDecision.Reject("case_not_appealable");
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > 2000)
            return AppealDecision.Reject("invalid_appeal_text");

        var appeal = new AppealState
        {
            Id = AppealId.New(),
            CaseId = moderationCase.Id,
            AuthorUserId = authorUserId,
            Text = text.Trim(),
            Status = AppealStatus.Open,
            CreatedAt = now,
        };
        return new AppealDecision(true, null, appeal, [new AppealOpened(appeal)]);
    }

    public static AppealDecision Resolve(
        ChatState chat,
        MemberState actor,
        MemberState target,
        ModerationCaseState moderationCase,
        AppealState appeal,
        bool approve,
        string? resolutionComment,
        DateTimeOffset now)
    {
        if (!chat.IsEnabled)
            return AppealDecision.Reject("chat_disabled");
        if (moderationCase.ChatId != chat.Id || moderationCase.TargetUserId != target.UserId
            || appeal.CaseId != moderationCase.Id || appeal.AuthorUserId != target.UserId)
            return AppealDecision.Reject("appeal_case_mismatch");

        var authorization = AuthorizationPolicy.Authorize(chat, actor, target, Permission.AppealsResolve);
        if (!authorization.Allowed)
            return AppealDecision.Reject(authorization.ErrorCode!);
        if (appeal.Status is not (AppealStatus.Open or AppealStatus.Reviewing))
            return AppealDecision.Reject("appeal_not_open");

        var resolved = appeal with
        {
            Status = approve ? AppealStatus.Approved : AppealStatus.Rejected,
            ResolvedBy = actor.UserId,
            ResolutionComment = string.IsNullOrWhiteSpace(resolutionComment) ? null : resolutionComment.Trim(),
            ResolvedAt = now,
        };
        DomainEvent domainEvent = approve ? new AppealApproved(resolved) : new AppealRejected(resolved);
        return new AppealDecision(true, null, resolved, [domainEvent]);
    }
}
