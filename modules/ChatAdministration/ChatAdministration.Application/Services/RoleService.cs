using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public sealed class RoleService(IChatAdministrationStore store)
{
    public async Task<string> ListAsync(
        ChatId chatId,
        UserId actorUserId,
        UserId targetUserId,
        ChatMemberRole actorObservedRole,
        ChatMemberRole targetObservedRole,
        string actorDisplayName,
        string targetDisplayName,
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
        if (!AuthorizationPolicy.HasPermission(context.Chat, context.Actor, Permission.RolesView))
            return "🚫 Недостаточно прав для просмотра ролей.";

        var roles = context.Target.Roles
            .OrderByDescending(role => role)
            .Select(role => role.ToString());
        var customRoles = context.Target.CustomRoleIds
            .Select(id => context.Chat.Settings.CustomRoles.FirstOrDefault(role => role.Id == id))
            .Where(role => role is not null)
            .Select(role => $"{role!.DisplayName} (custom:{role.Id.Value})");
        return $"👤 Роли {targetDisplayName}: {string.Join(", ", roles.Concat(customRoles))}";
    }

    public async Task<ModerationCommandResult> ChangeAsync(
        RoleMutationCommand command,
        ChatMemberRole actorObservedRole,
        ChatMemberRole targetObservedRole,
        string actorDisplayName,
        string targetDisplayName,
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
        var decision = command.CustomRoleId is { } customRoleId
            ? RolePolicy.ChangeCustom(context.Chat, context.Actor, context.Target, customRoleId, command.Assign)
            : RolePolicy.Change(context.Chat, context.Actor, context.Target, command.Role, command.Assign);
        if (!decision.Accepted)
        {
            var response = decision.ErrorCode switch
            {
                "permission_denied" => "🚫 Недостаточно прав для управления ролями.",
                "target_role_too_high" or "owner_protected" => "🚫 Нельзя менять роли пользователя с равной или более высокой ролью.",
                "role_too_high" => "🚫 Нельзя выдать роль не ниже собственной.",
                "role_already_assigned" => "ℹ️ Такая роль уже назначена.",
                "role_not_assigned" => "ℹ️ Такой роли у пользователя нет.",
                "custom_role_not_found" => "ℹ️ Custom role не найден.",
                "chat_disabled" => "🚫 Чат отключён.",
                _ => "Не удалось изменить роль.",
            };
            await store.EnqueueResponseAsync(command.ChatId, response, command.SourceMessageId, ct);
            return new ModerationCommandResult(false, false, decision.ErrorCode, null, response);
        }

        var result = await store.PersistRoleMutationAsync(command with
        {
            ResultMember = decision.Member!,
            Event = decision.Events.Single(),
        }, ct);
        var responseText = result.Duplicate ? "Команда уже обработана." : command.ResponseText;
        await store.EnqueueResponseAsync(command.ChatId, responseText, command.SourceMessageId, ct);
        return new ModerationCommandResult(true, result.Duplicate, null, null, responseText);
    }
}
