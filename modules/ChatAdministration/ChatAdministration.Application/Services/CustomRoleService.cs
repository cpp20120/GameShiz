using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public sealed class CustomRoleService(IChatAdministrationStore store)
{
    public async Task<string> ExecuteAsync(CustomRoleMutationCommand command, CancellationToken ct)
    {
        var context = await store.LoadContextAsync(
            command.ChatId,
            command.ActorUserId,
            command.ActorUserId,
            command.ActorObservedRole,
            command.ActorObservedRole,
            command.ActorDisplayName,
            command.ActorDisplayName,
            ct);
        var decision = CustomRolePolicy.Change(
            context.Chat,
            context.Actor,
            command.RoleId,
            command.DisplayName,
            command.Rank,
            command.Permissions,
            command.Remove);
        if (!decision.Accepted)
        {
            var response = decision.ErrorCode switch
            {
                "permission_denied" => "🚫 Недостаточно прав для управления custom roles.",
                "custom_role_not_found" => "ℹ️ Custom role не найден.",
                "role_too_high" => "🚫 Rank custom role должен быть ниже вашей роли.",
                "invalid_role_id" => "ℹ️ Некорректный ID роли.",
                "invalid_role_name" => "ℹ️ Некорректное имя роли.",
                "invalid_role_rank" => "ℹ️ Rank должен быть от 1 до 99.",
                "chat_disabled" => "🚫 Чат отключён.",
                _ => "Не удалось изменить custom role.",
            };
            await store.EnqueueResponseAsync(command.ChatId, response, command.SourceMessageId, ct);
            return response;
        }

        await store.UpdateChatSettingsAsync(
            command.ChatId,
            decision.Settings!,
            command.ActorUserId,
            command.CorrelationId,
            ct);
        var success = command.Remove
            ? $"✅ Custom role <code>{command.RoleId.Value}</code> удалена."
            : $"✅ Custom role <code>{command.RoleId.Value}</code> сохранена.";
        await store.EnqueueResponseAsync(command.ChatId, success, command.SourceMessageId, ct);
        return success;
    }
}
