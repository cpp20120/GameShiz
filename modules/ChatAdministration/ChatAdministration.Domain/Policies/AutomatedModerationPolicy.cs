using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public static class AutomatedModerationPolicy
{
    public static AutomatedModerationDecision Decide(
        ModerationMessageContext context,
        IReadOnlyCollection<IModerationRule> rules)
    {
        if (!context.Chat.IsEnabled || !context.Chat.Settings.AutoModerationEnabled || context.Message.IsServiceMessage)
            return AutomatedModerationDecision.Ignore("automod_disabled");

        var violations = rules
            .Select(rule => rule.Evaluate(context))
            .Where(violation => violation is not null)
            .Select(violation => violation!)
            .ToList();
        if (violations.Count == 0)
            return AutomatedModerationDecision.Ignore();

        var score = violations.Sum(violation => violation.Score);
        var escalation = context.Chat.Settings.ModerationEscalation;
        if (score < Math.Max(1, escalation.DeleteThreshold))
            return AutomatedModerationDecision.Ignore();

        var shouldWarn = score >= Math.Max(1, escalation.WarningThreshold);
        var action = score >= Math.Max(1, escalation.BanThreshold)
            ? ModerationAction.Ban
            : score >= Math.Max(1, escalation.MuteThreshold)
                ? ModerationAction.Mute
                : ModerationAction.Delete;
        var warningWillReachLimit = shouldWarn
            && context.Chat.Settings.WarningLimit > 0
            && context.Author.ActiveWarningCount + 1 >= context.Chat.Settings.WarningLimit;
        if (warningWillReachLimit && action == ModerationAction.Delete)
            action = context.Chat.Settings.WarningLimitAction is ModerationAction.Mute or ModerationAction.Ban
                ? context.Chat.Settings.WarningLimitAction
                : action;

        var duration = action switch
        {
            ModerationAction.Mute => escalation.MuteDuration,
            ModerationAction.Ban => escalation.BanDuration,
            _ => null,
        };
        var caseId = ModerationCaseId.New();
        DateTimeOffset? expiresAt = duration is { } value ? context.Message.SentAt.Add(value) : null;
        var reason = string.Join(", ", violations.Select(violation => violation.Code));
        var moderationCase = new ModerationCaseState
        {
            Id = caseId,
            ChatId = context.Message.ChatId,
            TargetUserId = context.Message.AuthorId,
            ActorType = ModerationActorType.AutoMod,
            Action = action,
            Reason = reason,
            SourceMessageId = context.Message.MessageId,
            SourceRuleId = violations[0].RuleId,
            CreatedAt = context.Message.SentAt,
            ExpiresAt = expiresAt,
            CorrelationId = $"automod:{context.Message.ChatId}:{context.Message.MessageId}",
        };

        var events = violations
            .Select(violation => (IDomainEvent)new ViolationDetected(
                context.Message.ChatId,
                context.Message.AuthorId,
                context.Message.MessageId,
                violation))
            .Append(new ModerationCaseCreated(moderationCase))
            .ToList();
        WarningState? warning = null;
        if (shouldWarn)
        {
            warning = new WarningState
            {
                Id = WarningId.New(),
                ChatId = context.Message.ChatId,
                TargetUserId = context.Message.AuthorId,
                Reason = reason,
                CreatedAt = context.Message.SentAt,
            };
            events.Add(new WarningIssued(warning));
            if (warningWillReachLimit)
                events.Add(new WarningLimitReached(
                    context.Message.ChatId,
                    context.Message.AuthorId,
                    context.Author.ActiveWarningCount + 1,
                    action));
        }
        var effects = new List<PlannedEffect>();
        var deleteId = EffectId.New();
        if (context.Chat.Settings.FloodPolicy.DeleteMessages)
        {
            effects.Add(new PlannedEffect(
                new DeleteMessageEffect(
                    context.Message.ChatId,
                    context.Message.MessageId,
                    caseId,
                    moderationCase.CorrelationId,
                    moderationCase.CorrelationId,
                    context.Message.AuthorId),
                EffectImportance.Required,
                [],
                Id: deleteId));
        }

        PlannedEffect? requiredAction = null;
        if (action == ModerationAction.Mute)
        {
            requiredAction = new PlannedEffect(
                new RestrictMemberEffect(
                    context.Message.ChatId,
                    context.Message.AuthorId,
                    expiresAt.GetValueOrDefault(context.Message.SentAt.Add(escalation.MuteDuration)),
                    caseId,
                    moderationCase.CorrelationId,
                    moderationCase.CorrelationId),
                EffectImportance.Required,
                []);
            effects.Add(requiredAction);
        }
        else if (action == ModerationAction.Ban)
        {
            var ban = new PlannedEffect(
                new BanMemberEffect(
                    context.Message.ChatId,
                    context.Message.AuthorId,
                    expiresAt,
                    caseId,
                    moderationCase.CorrelationId,
                    moderationCase.CorrelationId),
                EffectImportance.Required,
                [],
                Id: EffectId.New());
            effects.Add(ban);
            if (expiresAt is not null)
            {
                effects.Add(new PlannedEffect(
                    new UnbanMemberEffect(
                        context.Message.ChatId,
                        context.Message.AuthorId,
                        caseId,
                        expiresAt,
                        moderationCase.CorrelationId,
                        moderationCase.CorrelationId),
                    EffectImportance.Required,
                    [ban.Id!.Value]));
            }
        }

        return new AutomatedModerationDecision(true, null, violations, moderationCase, events, new EffectPlan(effects), warning);
    }
}
