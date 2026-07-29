using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed class PurgeService(IChatAdministrationStore store)
{
    public async Task<ModerationCommandResult> ExecuteAsync(
        PurgeMessagesCommand command,
        CancellationToken ct)
    {
        var context = await store.LoadContextAsync(
            command.ChatId,
            command.ActorUserId,
            command.ActorUserId,
            command.ActorObservedRole,
            command.ActorObservedRole,
            "moderator",
            "moderator",
            ct);
        if (!context.Chat.IsEnabled || !context.Chat.Settings.ManualModerationEnabled)
            return await RejectAsync(command, "moderation_disabled", "🚫 Модерация отключена в этом чате.", ct);
        if (!AuthorizationPolicy.HasPermission(context.Chat, context.Actor, Permission.MessagesPurge))
            return await RejectAsync(command, "permission_denied", "🚫 Недостаточно прав для очистки сообщений.", ct);

        var messageIds = await store.ListMessageIdsAsync(command.ChatId, command.TargetUserId, command.Count, ct);
        if (!messageIds.Contains(command.SourceMessageId))
            messageIds = messageIds.Append(command.SourceMessageId).Distinct().Take(command.Count + 1).ToArray();
        if (messageIds.Count == 0)
            return await RejectAsync(command, "messages_not_found", "ℹ️ В индексе нет сообщений для удаления.", ct);

        var effect = new DeleteMessagesEffect(
            command.ChatId,
            messageIds,
            null,
            command.CorrelationId,
            command.CausationId);
        var result = await store.PersistPurgeAsync(command, effect, messageIds.Count, ct);
        var response = result.Duplicate ? "Команда уже обработана." : $"🧹 Запланировано удаление сообщений: {messageIds.Count}.";
        await store.EnqueueResponseAsync(command.ChatId, response, command.SourceMessageId, ct);
        return new ModerationCommandResult(true, result.Duplicate, null, null, response);
    }

    private async Task<ModerationCommandResult> RejectAsync(
        PurgeMessagesCommand command,
        string errorCode,
        string response,
        CancellationToken ct)
    {
        await store.EnqueueResponseAsync(command.ChatId, response, command.SourceMessageId, ct);
        return new ModerationCommandResult(false, false, errorCode, null, response);
    }
}
