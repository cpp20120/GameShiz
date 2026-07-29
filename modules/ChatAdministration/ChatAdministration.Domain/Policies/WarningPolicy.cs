using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public static class WarningPolicy
{
    public static WarningDecision Revoke(
        ChatState chat,
        MemberState actor,
        MemberState target,
        WarningState warning,
        WarningRevocationReason reason)
    {
        if (!chat.IsEnabled || !chat.Settings.ManualModerationEnabled)
            return WarningDecision.Reject("moderation_disabled");
        if (warning.ChatId != chat.Id || warning.TargetUserId != target.UserId)
            return WarningDecision.Reject("warning_target_mismatch");
        if (!warning.IsActive)
            return WarningDecision.Reject("warning_not_active");

        var authorization = AuthorizationPolicy.Authorize(chat, actor, target, Permission.MembersRemoveWarning);
        if (!authorization.Allowed)
            return WarningDecision.Reject(authorization.ErrorCode!);

        var revoked = warning with
        {
            IsActive = false,
            RevocationReason = reason,
        };
        return new WarningDecision(
            true,
            null,
            revoked,
            [new WarningRevoked(warning.ChatId, warning.TargetUserId, warning.Id, reason)]);
    }
}
