using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public static class ManualModerationPolicy
{
    private static readonly IReadOnlyDictionary<ModerationAction, string> ResponseLabels = new Dictionary<ModerationAction, string>
    {
        [ModerationAction.Warn] = "⚠️ Предупреждение пользователя принято.",
        [ModerationAction.Unmute] = "🔊 Запрос на снятие ограничения принят.",
        [ModerationAction.Ban] = "🔨 Запрос на бан принят.",
        [ModerationAction.Unban] = "🔓 Запрос на разблокировку принят.",
        [ModerationAction.Kick] = "👢 Запрос на удаление пользователя принят.",
    };

    public static ManualModerationDecision Decide(
        ManualModerationRequest request,
        ChatState chat,
        MemberState actor,
        MemberState target)
    {
        if (!chat.IsEnabled || !chat.Settings.ManualModerationEnabled)
            return ManualModerationDecision.Reject("moderation_disabled");

        if (request.Action == ModerationAction.Mute)
            return ManualModerationDecision.Reject("use_mute_policy");

        var permission = PermissionFor(request.Action);
        var authorization = AuthorizationPolicy.Authorize(chat, actor, target, permission);
        if (!authorization.Allowed)
            return ManualModerationDecision.Reject(authorization.ErrorCode!);

        var reason = NormalizeReason(request.Reason);
        if (ReasonRequired(request.Action, chat.Settings) && reason is null)
            return ManualModerationDecision.Reject("reason_required");

        var durationError = ValidateDuration(request.Action, request.Duration);
        if (durationError is not null)
            return ManualModerationDecision.Reject(durationError);

        var caseId = ModerationCaseId.New();
        var warningWillReachLimit = request.Action == ModerationAction.Warn
            && chat.Settings.WarningLimit > 0
            && target.ActiveWarningCount + 1 >= chat.Settings.WarningLimit;
        var escalatedAction = warningWillReachLimit
            ? chat.Settings.WarningLimitAction
            : request.Action;
        var escalationDuration = warningWillReachLimit
            ? chat.Settings.WarningLimitMuteDuration
            : request.Duration;
        if (escalatedAction == ModerationAction.Mute && escalationDuration is null)
            escalationDuration = TimeSpan.FromMinutes(10);
        var moderationCase = new ModerationCaseState
        {
            Id = caseId,
            ChatId = request.ChatId,
            TargetUserId = request.TargetUserId,
            ActorUserId = request.ActorUserId,
            Action = escalatedAction,
            Reason = reason,
            CreatedAt = request.Now,
            ExpiresAt = escalationDuration is { } duration ? request.Now.Add(duration) : null,
            CorrelationId = request.CorrelationId,
        };

        var events = new List<IDomainEvent> { new ModerationCaseCreated(moderationCase) };
        WarningState? warning = null;
        if (request.Action == ModerationAction.Warn)
        {
            warning = new WarningState
            {
                Id = WarningId.New(),
                ChatId = request.ChatId,
                TargetUserId = request.TargetUserId,
                ActorUserId = request.ActorUserId,
                Reason = reason,
                CreatedAt = request.Now,
            };
            events.Add(new WarningIssued(warning));
            if (warningWillReachLimit)
                events.Add(new WarningLimitReached(request.ChatId, request.TargetUserId, target.ActiveWarningCount + 1, escalatedAction));
        }

        var effects = new List<PlannedEffect>();
        var requiredEffectId = EffectId.New();
        var required = CreateRequiredEffect(request with { Duration = escalationDuration, Action = escalatedAction }, caseId);
        if (required is not null)
            effects.Add(new PlannedEffect(required, EffectImportance.Required, [], Id: requiredEffectId));

        if (escalatedAction is ModerationAction.Ban && escalationDuration is not null)
        {
            var expiration = new UnbanMemberEffect(
                request.ChatId,
                request.TargetUserId,
                caseId,
                request.Now.Add(escalationDuration.Value),
                request.CorrelationId,
                request.CausationId);
            effects.Add(new PlannedEffect(expiration, EffectImportance.Required, [requiredEffectId]));
        }

        var response = new SendMessageEffect(
            request.ChatId,
            ResponseFor(request.Action, request.Duration, reason),
            request.SourceMessageId,
            MessageParseMode.Html);
        effects.Add(new PlannedEffect(response, EffectImportance.BestEffort, []));

        return new ManualModerationDecision(
            true,
            null,
            moderationCase,
            warning,
            events,
            new EffectPlan(effects));
    }

    private static Permission PermissionFor(ModerationAction action) => action switch
    {
        ModerationAction.Warn => Permission.MembersWarn,
        ModerationAction.Unmute => Permission.MembersUnmute,
        ModerationAction.Ban => Permission.MembersBan,
        ModerationAction.Unban => Permission.MembersUnban,
        ModerationAction.Kick => Permission.MembersKick,
        _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported manual moderation action."),
    };

    private static IModerationEffect? CreateRequiredEffect(ManualModerationRequest request, ModerationCaseId caseId) => request.Action switch
    {
        ModerationAction.Mute => new RestrictMemberEffect(
            request.ChatId,
            request.TargetUserId,
            request.Now.Add(request.Duration.GetValueOrDefault(TimeSpan.FromMinutes(10))),
            caseId,
            request.CorrelationId,
            request.CausationId),
        ModerationAction.Unmute => new UnrestrictMemberEffect(
            request.ChatId,
            request.TargetUserId,
            caseId,
            null,
            request.CorrelationId,
            request.CausationId),
        ModerationAction.Ban => new BanMemberEffect(
            request.ChatId,
            request.TargetUserId,
            request.Duration is { } duration ? request.Now.Add(duration) : null,
            caseId,
            request.CorrelationId,
            request.CausationId),
        ModerationAction.Unban => new UnbanMemberEffect(
            request.ChatId,
            request.TargetUserId,
            caseId,
            null,
            request.CorrelationId,
            request.CausationId),
        ModerationAction.Kick => new KickMemberEffect(
            request.ChatId,
            request.TargetUserId,
            caseId,
            request.CorrelationId,
            request.CausationId),
        _ => null,
    };

    private static string? NormalizeReason(string? reason) =>
        string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();

    private static bool ReasonRequired(ModerationAction action, ChatSettings settings) => action switch
    {
        ModerationAction.Warn => settings.RequireReasonForWarn,
        ModerationAction.Ban => settings.RequireReasonForBan,
        ModerationAction.Kick => settings.RequireReasonForKick,
        _ => false,
    };

    private static string? ValidateDuration(ModerationAction action, TimeSpan? duration) =>
        action == ModerationAction.Ban && duration is { } value && value <= TimeSpan.Zero
            ? "invalid_duration"
            : null;

    private static string ResponseFor(ModerationAction action, TimeSpan? duration, string? reason)
    {
        var label = ResponseLabels[action];
        if (action == ModerationAction.Ban && duration is { } value)
            label = $"🔨 Запрос на бан принят на {FormatDuration(value)}.";
        return reason is null ? label : $"{label} Причина: {reason}";
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalDays >= 1 && duration.TotalDays % 1 == 0)
            return $"{duration.TotalDays:0} дн.";
        if (duration.TotalHours >= 1 && duration.TotalHours % 1 == 0)
            return $"{duration.TotalHours:0} ч.";
        if (duration.TotalMinutes >= 1 && duration.TotalMinutes % 1 == 0)
            return $"{duration.TotalMinutes:0} мин.";
        return $"{duration.TotalSeconds:0} сек.";
    }
}
