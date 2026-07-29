using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public sealed class WarningService(IChatAdministrationStore store)
{
    public async Task<WarningListResult> ListAsync(
        ChatId chatId,
        UserId actorUserId,
        UserId targetUserId,
        ChatMemberRole actorObservedRole,
        ChatMemberRole targetObservedRole,
        string actorDisplayName,
        string targetDisplayName,
        bool activeOnly,
        CancellationToken ct)
    {
        var context = await store.LoadContextAsync(
            chatId,
            actorUserId,
            targetUserId,
            actorObservedRole,
            targetObservedRole,
            actorDisplayName,
            targetDisplayName,
            ct);
        if (!AuthorizationPolicy.HasPermission(context.Chat, context.Actor, Permission.MembersViewWarnings))
        {
            return new WarningListResult(false, "permission_denied", [], "🚫 Недостаточно прав для просмотра предупреждений.");
        }

        var warnings = await store.ListWarningsAsync(chatId, targetUserId, activeOnly, ct);
        return new WarningListResult(true, null, warnings, Render(warnings, targetDisplayName));
    }

    public async Task<ModerationCommandResult> RevokeAsync(
        WarningMutationCommand command,
        ChatMemberRole actorObservedRole,
        ChatMemberRole targetObservedRole,
        string actorDisplayName,
        string targetDisplayName,
        WarningId? warningId,
        WarningRevocationReason reason,
        CancellationToken ct)
    {
        var context = await store.LoadContextAsync(
            command.ChatId,
            command.ActorUserId,
            command.TargetUserId,
            actorObservedRole,
            targetObservedRole,
            actorDisplayName,
            targetDisplayName,
            ct);
        var warnings = await store.ListWarningsAsync(command.ChatId, command.TargetUserId, true, ct);
        if (warningId is not null)
            warnings = warnings.Where(warning => warning.Id == warningId.Value).ToArray();
        if (warnings.Count == 0)
        {
            const string response = "ℹ️ Активных предупреждений не найдено.";
            await store.EnqueueResponseAsync(command.ChatId, response, command.SourceMessageId, ct);
            return new ModerationCommandResult(false, false, "warning_not_found", null, response);
        }

        var events = new List<DomainEvent>();
        var revoked = new List<WarningState>();
        foreach (var warning in warnings)
        {
            var decision = WarningPolicy.Revoke(context.Chat, context.Actor, context.Target, warning, reason);
            if (!decision.Accepted)
            {
                const string denied = "🚫 Нельзя снять предупреждение у этого пользователя.";
                await store.EnqueueResponseAsync(command.ChatId, denied, command.SourceMessageId, ct);
                return new ModerationCommandResult(false, false, decision.ErrorCode, null, denied);
            }

            revoked.Add(decision.Warning!);
            events.AddRange(decision.Events);
        }

        var result = await store.PersistWarningMutationAsync(
            command with
            {
                Warnings = revoked,
                Events = events,
                ResponseText = warningId is null
                    ? $"✅ Снято предупреждений: {revoked.Count}."
                    : "✅ Предупреждение снято.",
            },
            ct);
        var responseText = result.Duplicate ? "Команда уже обработана." : command.ResponseText;
        await store.EnqueueResponseAsync(command.ChatId, responseText, command.SourceMessageId, ct);
        return new ModerationCommandResult(true, result.Duplicate, null, null, responseText);
    }

    private static string Render(IReadOnlyList<WarningState> warnings, string targetDisplayName)
    {
        if (warnings.Count == 0)
            return $"✅ У пользователя {targetDisplayName} нет активных предупреждений.";

        var lines = warnings.Select((warning, index) =>
            $"{index + 1}. <code>{warning.Id}</code> — {warning.Reason ?? "без причины"}");
        return $"⚠️ Активные предупреждения {targetDisplayName}: {warnings.Count}\n{string.Join('\n', lines)}";
    }
}
