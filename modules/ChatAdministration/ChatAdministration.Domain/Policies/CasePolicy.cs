using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public static class CasePolicy
{
    public static CaseRevocationDecision Revoke(
        ChatState chat,
        MemberState actor,
        MemberState target,
        ModerationCaseState moderationCase,
        string correlationId,
        string causationId)
    {
        if (!chat.IsEnabled)
            return CaseRevocationDecision.Reject("chat_disabled");
        if (moderationCase.ChatId != chat.Id || moderationCase.TargetUserId != target.UserId)
            return CaseRevocationDecision.Reject("case_target_mismatch");

        var authorization = AuthorizationPolicy.Authorize(chat, actor, target, Permission.CasesResolve);
        if (!authorization.Allowed)
            return CaseRevocationDecision.Reject(authorization.ErrorCode!);

        if (moderationCase.Status is not (ModerationCaseStatus.Applied
            or ModerationCaseStatus.PartiallyApplied
            or ModerationCaseStatus.Unknown))
            return CaseRevocationDecision.Reject("case_not_revivable");

        var externalEffectId = EffectId.New();
        var markerEffectId = EffectId.New();
        IModerationEffect? externalEffect = moderationCase.Action switch
        {
            ModerationAction.Mute => new UnrestrictMemberEffect(
                chat.Id,
                target.UserId,
                moderationCase.Id,
                moderationCase.ExpiresAt,
                correlationId,
                causationId),
            ModerationAction.Ban => new UnbanMemberEffect(
                chat.Id,
                target.UserId,
                moderationCase.Id,
                moderationCase.ExpiresAt,
                correlationId,
                causationId),
            _ => null,
        };
        if (externalEffect is null)
            return CaseRevocationDecision.Reject("case_action_not_revivable");

        var updatedCase = moderationCase with { Status = ModerationCaseStatus.Revoking };
        return new CaseRevocationDecision(
            true,
            null,
            updatedCase,
            [new ModerationCaseRevocationRequested(updatedCase)],
            new EffectPlan(
            [
                new PlannedEffect(externalEffect, EffectImportance.Required, [], Id: externalEffectId),
                new PlannedEffect(
                    new MarkModerationCaseRevokedEffect(moderationCase.Id, correlationId, causationId),
                    EffectImportance.Required,
                    [externalEffectId],
                    Id: markerEffectId),
            ]));
    }
}
