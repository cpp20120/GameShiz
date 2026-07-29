using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public static class MutePolicy
{
    public static MuteDecision Decide(
        MuteRequest request,
        ChatState chat,
        MemberState actor,
        MemberState target)
    {
        if (!chat.IsEnabled || !chat.Settings.ManualModerationEnabled)
            return MuteDecision.Reject("moderation_disabled");
        if (request.Duration <= TimeSpan.Zero)
            return MuteDecision.Reject("invalid_duration");
        if (chat.Settings.RequireReasonForMute && string.IsNullOrWhiteSpace(request.Reason))
            return MuteDecision.Reject("reason_required");

        var authorization = AuthorizationPolicy.Authorize(chat, actor, target, Permission.MembersMute);
        if (!authorization.Allowed)
            return MuteDecision.Reject(authorization.ErrorCode!);

        var caseId = ModerationCaseId.New();
        var expiresAt = request.Now.Add(request.Duration);
        var desired = new RestrictionState { CanSendMessages = false, Until = expiresAt };
        var moderationCase = new ModerationCaseState
        {
            Id = caseId,
            ChatId = request.ChatId,
            TargetUserId = request.TargetUserId,
            ActorUserId = request.ActorUserId,
            Action = ModerationAction.Mute,
            Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            CreatedAt = request.Now,
            ExpiresAt = expiresAt,
            CorrelationId = request.CorrelationId,
        };

        var restrictionEffect = new RestrictMemberEffect(
            request.ChatId,
            request.TargetUserId,
            expiresAt,
            caseId,
            request.CorrelationId,
            request.CausationId);
        var restrictionEffectId = EffectId.New();
        var expirationEffect = new UnrestrictMemberEffect(
            request.ChatId,
            request.TargetUserId,
            caseId,
            expiresAt,
            request.CorrelationId,
            request.CausationId);
        var responseText = string.IsNullOrWhiteSpace(request.Reason)
            ? "🔇 Запрос на ограничение пользователя принят."
            : $"🔇 Запрос на ограничение пользователя принят на {FormatDuration(request.Duration)}: {request.Reason.Trim()}";
        var responseEffect = new SendMessageEffect(
            request.ChatId,
            responseText,
            request.SourceMessageId,
            MessageParseMode.Html);

        return new MuteDecision(
            true,
            null,
            moderationCase,
            desired,
            [
                new ModerationCaseCreated(moderationCase),
                new RestrictionDesiredStateChanged(request.ChatId, request.TargetUserId, desired, caseId),
            ],
            new EffectPlan(
            [
                new PlannedEffect(restrictionEffect, EffectImportance.Required, [], Id: restrictionEffectId),
                new PlannedEffect(expirationEffect, EffectImportance.Required, [restrictionEffectId]),
                new PlannedEffect(responseEffect, EffectImportance.BestEffort, []),
            ]));
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 7 && duration.TotalDays % 7 == 0)
            return $"{duration.TotalDays / 7:0} нед.";
        if (duration.TotalDays >= 1 && duration.TotalDays % 1 == 0)
            return $"{duration.TotalDays:0} дн.";
        if (duration.TotalHours >= 1 && duration.TotalHours % 1 == 0)
            return $"{duration.TotalHours:0} ч.";
        if (duration.TotalMinutes >= 1 && duration.TotalMinutes % 1 == 0)
            return $"{duration.TotalMinutes:0} мин.";
        return $"{duration.TotalSeconds:0} сек.";
    }
}
