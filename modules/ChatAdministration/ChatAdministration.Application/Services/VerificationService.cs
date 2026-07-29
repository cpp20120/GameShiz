using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public sealed class VerificationService(IChatAdministrationStore store)
{
    public async Task<VerificationPersistenceResult> StartAsync(
        ChatId chatId,
        UserId userId,
        string displayName,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var context = await store.LoadContextAsync(
            chatId,
            userId,
            userId,
            ChatMemberRole.Member,
            ChatMemberRole.Member,
            displayName,
            displayName,
            ct);
        var decision = VerificationPolicy.Start(context.Chat, context.Target, ["✅", "❌", "🤖"], "✅", now);
        if (!decision.Accepted)
            return new VerificationPersistenceResult(false, false);

        var session = decision.Session!;
        return await store.PersistVerificationAsync(
            session,
            VerificationStatus.Pending,
            decision.Events,
            decision.EffectPlan,
            $"captcha:{session.Id}",
            $"member-joined:{userId}",
            ct);
    }

    public async Task<VerificationPersistenceResult> SubmitAsync(
        VerificationSessionId sessionId,
        UserId actorUserId,
        string callbackQueryId,
        string answer,
        int challengeMessageId,
        DateTimeOffset now,
        CancellationToken ct,
        ChatId? callbackChatId = null)
    {
        var session = await store.LoadVerificationAsync(sessionId, ct);
        if (session is null)
            return new VerificationPersistenceResult(false, false);
        if (callbackChatId is not null && callbackChatId != session.ChatId)
            return new VerificationPersistenceResult(false, false);
        if (session.ChallengeMessageId is { } expectedMessageId && expectedMessageId != challengeMessageId)
            return new VerificationPersistenceResult(false, false);
        var context = await store.LoadContextAsync(
            session.ChatId,
            actorUserId,
            session.UserId,
            ChatMemberRole.Member,
            ChatMemberRole.Member,
            "member",
            "member",
            ct);
        var decision = VerificationPolicy.Submit(
            session,
            context.Chat,
            actorUserId,
            callbackQueryId,
            answer,
            challengeMessageId,
            now);
        if (!decision.Accepted)
            return new VerificationPersistenceResult(false, false);

        if (decision.Session?.Status == VerificationStatus.Passed && context.Chat.Settings.WelcomeEnabled)
        {
            var effects = decision.EffectPlan.Effects.ToList();
            effects.Add(new PlannedEffect(
                MemberLifecyclePolicy.CreateWelcomeEffect(context.Chat, context.Target),
                EffectImportance.BestEffort,
                []));
            decision = decision with { EffectPlan = new EffectPlan(effects) };
        }

        return await store.PersistVerificationAsync(
            decision.Session!,
            session.Status,
            decision.Events,
            decision.EffectPlan,
            $"captcha:{session.Id}",
            $"callback:{callbackQueryId}",
            ct);
    }

    public async Task<VerificationPersistenceResult> ExpireAsync(
        VerificationSession session,
        int challengeMessageId,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var context = await store.LoadContextAsync(
            session.ChatId,
            session.UserId,
            session.UserId,
            ChatMemberRole.Member,
            ChatMemberRole.Member,
            "member",
            "member",
            ct);
        var decision = VerificationPolicy.Expire(session, context.Chat, challengeMessageId, now);
        if (!decision.Accepted)
            return new VerificationPersistenceResult(false, false);
        return await store.PersistVerificationAsync(
            decision.Session!,
            session.Status,
            decision.Events,
            decision.EffectPlan,
            $"captcha:{session.Id}",
            $"verification-expire:{session.Id}",
            ct);
    }
}
