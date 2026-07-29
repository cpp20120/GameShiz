using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public sealed class AppealService(IChatAdministrationStore store)
{
    public async Task<ModerationCommandResult> OpenAsync(
        OpenAppealCommand command,
        string authorDisplayName,
        CancellationToken ct)
    {
        var moderationCase = await store.LoadCaseAsync(command.ChatId, command.CaseId, ct);
        if (moderationCase is null)
            return await RejectAsync(command.ChatId, command.SourceMessageId, "case_not_found", "ℹ️ Case не найден.", ct);

        var context = await store.LoadContextAsync(
            command.ChatId,
            command.AuthorUserId,
            command.AuthorUserId,
            ChatMemberRole.Member,
            ChatMemberRole.Member,
            authorDisplayName,
            authorDisplayName,
            ct);
        var decision = AppealPolicy.Open(context.Chat, moderationCase, command.AuthorUserId, command.Text, command.CreatedAt);
        if (!decision.Accepted)
        {
            var response = decision.ErrorCode switch
            {
                "appeal_author_mismatch" => "🚫 Обжаловать можно только собственный case.",
                "case_action_not_appealable" or "case_not_appealable" => "ℹ️ Этот case нельзя обжаловать.",
                _ => "Не удалось открыть appeal.",
            };
            return await RejectAsync(command.ChatId, command.SourceMessageId, decision.ErrorCode!, response, ct);
        }

        var result = await store.PersistAppealOpenAsync(command, decision, ct);
        var responseText = result.Duplicate ? "Appeal уже обработан." : "📨 Appeal отправлен модераторам.";
        await store.EnqueueResponseAsync(command.ChatId, responseText, command.SourceMessageId, ct);
        return new ModerationCommandResult(true, result.Duplicate, null, null, responseText);
    }

    public async Task<ModerationCommandResult> ResolveAsync(
        ResolveAppealCommand command,
        ChatMemberRole actorObservedRole,
        ChatMemberRole targetObservedRole,
        string actorDisplayName,
        string targetDisplayName,
        CancellationToken ct)
    {
        var appeal = await store.LoadAppealAsync(command.ChatId, command.AppealId, ct);
        if (appeal is null)
            return await RejectAsync(command.ChatId, command.SourceMessageId, "appeal_not_found", "ℹ️ Appeal не найден.", ct);
        var moderationCase = await store.LoadCaseAsync(command.ChatId, appeal.CaseId, ct);
        if (moderationCase is null)
            return await RejectAsync(command.ChatId, command.SourceMessageId, "case_not_found", "ℹ️ Case appeal не найден.", ct);

        var context = await store.LoadContextAsync(
            command.ChatId,
            command.ActorUserId,
            moderationCase.TargetUserId,
            actorObservedRole,
            targetObservedRole,
            actorDisplayName,
            targetDisplayName,
            ct);
        var decision = AppealPolicy.Resolve(
            context.Chat,
            context.Actor,
            context.Target,
            moderationCase,
            appeal,
            command.Approve,
            command.ResolutionComment,
            command.CreatedAt);
        if (!decision.Accepted)
            return await RejectAsync(command.ChatId, command.SourceMessageId, decision.ErrorCode!, "🚫 Не удалось обработать appeal.", ct);

        CaseRevocationDecision? revocation = null;
        if (command.Approve)
        {
            revocation = CasePolicy.Revoke(
                context.Chat,
                context.Actor,
                context.Target,
                moderationCase,
                command.CorrelationId,
                command.CausationId);
            if (!revocation.Accepted)
                return await RejectAsync(command.ChatId, command.SourceMessageId, revocation.ErrorCode!, "🚫 Не удалось отменить наказание по appeal.", ct);
        }

        var result = await store.PersistAppealResolutionAsync(command, decision, revocation, ct);
        var responseText = result.Duplicate
            ? "Appeal уже обработан."
            : command.Approve ? "✅ Appeal одобрен; отмена наказания поставлена в durable outbox." : "❌ Appeal отклонён.";
        await store.EnqueueResponseAsync(command.ChatId, responseText, command.SourceMessageId, ct);
        return new ModerationCommandResult(true, result.Duplicate, null, moderationCase.Id, responseText);
    }

    private async Task<ModerationCommandResult> RejectAsync(
        ChatId chatId,
        int? sourceMessageId,
        string errorCode,
        string response,
        CancellationToken ct)
    {
        await store.EnqueueResponseAsync(chatId, response, sourceMessageId, ct);
        return new ModerationCommandResult(false, false, errorCode, null, response);
    }
}
