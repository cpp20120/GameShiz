using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public static class VerificationPolicy
{
    public static VerificationDecision Start(
        ChatState chat,
        MemberState member,
        IReadOnlyCollection<string> options,
        string correctAnswer,
        DateTimeOffset now)
    {
        if (!chat.IsEnabled || !chat.Settings.CaptchaPolicy.Enabled)
            return VerificationDecision.Reject("captcha_disabled");
        if (chat.Settings.CaptchaPolicy.Timeout <= TimeSpan.Zero)
            return VerificationDecision.Reject("invalid_timeout");
        if (chat.Settings.CaptchaPolicy.MaximumAttempts <= 0)
            return VerificationDecision.Reject("invalid_attempt_limit");

        var normalizedOptions = options
            .Where(option => !string.IsNullOrWhiteSpace(option))
            .Select(option => option.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (normalizedOptions.Length == 0 || !normalizedOptions.Contains(correctAnswer, StringComparer.Ordinal))
            return VerificationDecision.Reject("invalid_challenge");

        var session = new VerificationSession
        {
            Id = VerificationSessionId.New(),
            ChatId = chat.Id,
            UserId = member.UserId,
            CorrectAnswer = correctAnswer,
            Options = normalizedOptions,
            MaximumAttempts = chat.Settings.CaptchaPolicy.MaximumAttempts,
            CreatedAt = now,
            ExpiresAt = now.Add(chat.Settings.CaptchaPolicy.Timeout),
        };
        var buttons = normalizedOptions
            .Select(option => (IReadOnlyList<InlineKeyboardButtonSpec>)[
                new InlineKeyboardButtonSpec(option, $"captcha:{session.Id}:{option}")])
            .ToArray();
        var restrictionId = EffectId.New();
        var effects = new PlannedEffect[]
        {
            new(
                new RestrictMemberEffect(chat.Id, member.UserId, session.ExpiresAt, null, $"captcha:{session.Id}", $"member-joined:{member.UserId}"),
                EffectImportance.Required,
                [],
                Id: restrictionId),
            new(
                new SendMessageEffect(
                    chat.Id,
                    "🧩 Подтвердите, что вы не бот, нажав правильную кнопку.",
                    ParseMode: MessageParseMode.Html,
                    InlineKeyboard: new InlineKeyboardSpec(buttons)),
                EffectImportance.Required,
                [restrictionId]),
        };
        return new VerificationDecision(
            true,
            null,
            session,
            [new VerificationStarted(session)],
            new EffectPlan(effects));
    }

    public static VerificationDecision Submit(
        VerificationSession session,
        ChatState chat,
        UserId actorUserId,
        string callbackQueryId,
        string answer,
        int challengeMessageId,
        DateTimeOffset now)
    {
        if (session.Status != VerificationStatus.Pending)
            return VerificationDecision.Reject("verification_completed");
        if (session.ChatId != chat.Id || session.UserId != actorUserId)
            return VerificationDecision.Reject("verification_actor_mismatch");
        if (now >= session.ExpiresAt)
            return Expire(session, chat, challengeMessageId, now);
        if (!session.Options.Contains(answer, StringComparer.Ordinal))
            return VerificationDecision.Reject("invalid_answer");

        if (string.Equals(answer, session.CorrectAnswer, StringComparison.Ordinal))
        {
            var passed = session with { Status = VerificationStatus.Passed, ChallengeMessageId = challengeMessageId };
            var effects = new List<PlannedEffect>
            {
                new(new UnrestrictMemberEffect(session.ChatId, session.UserId, null, session.ExpiresAt, $"captcha:{session.Id}", $"captcha:{session.Id}"), EffectImportance.Required, []),
                new(new AnswerCallbackQueryEffect(callbackQueryId, "Проверка пройдена."), EffectImportance.BestEffort, []),
            };
            if (chat.Settings.CaptchaPolicy.DeleteChallengeAfterCompletion
                && challengeMessageId > 0
                && (session.ChallengeMessageId is null || session.ChallengeMessageId == challengeMessageId))
                effects.Add(new PlannedEffect(new DeleteMessageEffect(session.ChatId, challengeMessageId, null, $"captcha:{session.Id}", $"captcha:{session.Id}"), EffectImportance.BestEffort, []));
            return new VerificationDecision(true, null, passed, [new VerificationPassed(passed)], new EffectPlan(effects));
        }

        var attempts = session.Attempts + 1;
        var final = attempts >= session.MaximumAttempts;
        var failed = session with
        {
            Attempts = attempts,
            Status = final ? VerificationStatus.Failed : VerificationStatus.Pending,
            ChallengeMessageId = challengeMessageId,
        };
        var failedEffects = new List<PlannedEffect>
        {
            new(new AnswerCallbackQueryEffect(callbackQueryId, final ? "Проверка не пройдена." : "Неверный вариант."), EffectImportance.BestEffort, []),
        };
        if (final)
        {
            failedEffects.Insert(0, new PlannedEffect(
                chat.Settings.CaptchaPolicy.FailureAction == CaptchaFailureAction.Ban
                    ? new BanMemberEffect(session.ChatId, session.UserId, null, null, $"captcha:{session.Id}", $"captcha:{session.Id}")
                    : new KickMemberEffect(session.ChatId, session.UserId, null, $"captcha:{session.Id}", $"captcha:{session.Id}"),
                EffectImportance.Required,
                []));
            if (chat.Settings.CaptchaPolicy.DeleteChallengeAfterCompletion
                && challengeMessageId > 0
                && (session.ChallengeMessageId is null || session.ChallengeMessageId == challengeMessageId))
                failedEffects.Add(new PlannedEffect(new DeleteMessageEffect(session.ChatId, challengeMessageId, null, $"captcha:{session.Id}", $"captcha:{session.Id}"), EffectImportance.BestEffort, []));
        }
        return new VerificationDecision(true, null, failed, [new VerificationFailed(failed, final)], new EffectPlan(failedEffects));
    }

    public static VerificationDecision Expire(
        VerificationSession session,
        ChatState chat,
        int challengeMessageId,
        DateTimeOffset now)
    {
        if (session.Status != VerificationStatus.Pending)
            return VerificationDecision.Reject("verification_completed");
        if (now < session.ExpiresAt)
            return VerificationDecision.Reject("verification_not_expired");

        var expired = session with { Status = VerificationStatus.Expired, ChallengeMessageId = challengeMessageId };
        var punishment = chat.Settings.CaptchaPolicy.FailureAction == CaptchaFailureAction.Ban
            ? (IModerationEffect)new BanMemberEffect(session.ChatId, session.UserId, null, null, $"captcha:{session.Id}", $"captcha:{session.Id}")
            : new KickMemberEffect(session.ChatId, session.UserId, null, $"captcha:{session.Id}", $"captcha:{session.Id}");
        var effects = new List<PlannedEffect> { new(punishment, EffectImportance.Required, []) };
        if (chat.Settings.CaptchaPolicy.DeleteChallengeAfterCompletion
            && challengeMessageId > 0
            && (session.ChallengeMessageId is null || session.ChallengeMessageId == challengeMessageId))
            effects.Add(new PlannedEffect(new DeleteMessageEffect(session.ChatId, challengeMessageId, null, $"captcha:{session.Id}", $"captcha:{session.Id}"), EffectImportance.BestEffort, []));
        return new VerificationDecision(true, null, expired, [new VerificationExpired(expired)], new EffectPlan(effects));
    }
}
