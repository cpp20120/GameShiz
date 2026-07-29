using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public sealed class CaseService(IChatAdministrationStore store)
{
    public async Task<CaseListResult> ListAsync(ModerationCaseQuery query, CancellationToken ct)
    {
        var targetUserId = query.TargetUserId ?? query.ActorUserId;
        var context = await store.LoadContextAsync(
            query.ChatId,
            query.ActorUserId,
            targetUserId,
            query.ActorObservedRole,
            query.TargetObservedRole,
            query.ActorDisplayName,
            query.TargetDisplayName,
            ct);
        if (!AuthorizationPolicy.HasPermission(context.Chat, context.Actor, Permission.CasesView))
        {
            return new CaseListResult(false, "permission_denied", [], "🚫 Недостаточно прав для просмотра cases.");
        }

        var cases = await store.ListCasesAsync(query.ChatId, query.TargetUserId, query.Limit, ct);
        return new CaseListResult(true, null, cases, Render(cases));
    }

    public async Task<ModerationCommandResult> RevokeAsync(
        RevokeModerationCaseCommand command,
        ChatMemberRole actorObservedRole,
        ChatMemberRole targetObservedRole,
        string actorDisplayName,
        string targetDisplayName,
        CancellationToken ct)
    {
        var moderationCase = await store.LoadCaseAsync(command.ChatId, command.CaseId, ct);
        if (moderationCase is null)
            return await RejectAsync(command, "case_not_found", "ℹ️ Case не найден.", ct);

        var context = await store.LoadContextAsync(
            command.ChatId,
            command.ActorUserId,
            moderationCase.TargetUserId,
            actorObservedRole,
            targetObservedRole,
            actorDisplayName,
            targetDisplayName,
            ct);
        var decision = CasePolicy.Revoke(
            context.Chat,
            context.Actor,
            context.Target,
            moderationCase,
            command.CorrelationId,
            command.CausationId);
        if (!decision.Accepted)
        {
            var response = decision.ErrorCode switch
            {
                "permission_denied" => "🚫 Недостаточно прав для отмены case.",
                "owner_protected" or "target_role_too_high" => "🚫 Нельзя отменить наказание пользователя с равной или более высокой ролью.",
                "case_not_revivable" => "ℹ️ Этот case уже завершён или не может быть отменён.",
                "case_action_not_revivable" => "ℹ️ Для этого типа case отмена не поддерживается.",
                _ => "Не удалось отменить case.",
            };
            return await RejectAsync(command, decision.ErrorCode!, response, ct);
        }

        var result = await store.PersistCaseRevocationAsync(command, decision, ct);
        var responseText = result.Duplicate
            ? "Команда уже обработана."
            : "♻️ Отмена case принята и будет применена durable worker-ом.";
        await store.EnqueueResponseAsync(command.ChatId, responseText, command.SourceMessageId, ct);
        return new ModerationCommandResult(true, result.Duplicate, null, command.CaseId, responseText);
    }

    private async Task<ModerationCommandResult> RejectAsync(
        RevokeModerationCaseCommand command,
        string errorCode,
        string response,
        CancellationToken ct)
    {
        await store.EnqueueResponseAsync(command.ChatId, response, command.SourceMessageId, ct);
        return new ModerationCommandResult(false, false, errorCode, null, response);
    }

    private static string Render(IReadOnlyList<ModerationCaseState> cases)
    {
        if (cases.Count == 0)
            return "ℹ️ Cases не найдены.";

        var lines = cases.Select(item =>
            $"<code>{item.Id}</code> — {item.Action.ToString().ToLowerInvariant()} — {item.Status.ToString().ToLowerInvariant()} — {item.TargetUserId}");
        return $"🗂 Cases: {cases.Count}\n{string.Join('\n', lines)}";
    }
}
