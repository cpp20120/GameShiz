using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Authorization;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed class LifecycleSettingsService(IChatAdministrationStore store)
{
    public async Task<string> ExecuteAsync(UpdateLifecycleSettingsCommand command, CancellationToken ct)
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
        if (!AuthorizationPolicy.HasPermission(context.Chat, context.Actor, Permission.ChatManageSettings))
        {
            const string denied = "🚫 Только администратор может изменять lifecycle-настройки.";
            await store.EnqueueResponseAsync(command.ChatId, denied, command.SourceMessageId, ct);
            return denied;
        }

        var current = context.Chat.Settings;
        var updated = current with
        {
            WelcomeEnabled = command.WelcomeEnabled ?? current.WelcomeEnabled,
            GoodbyeEnabled = command.GoodbyeEnabled ?? current.GoodbyeEnabled,
            WelcomeTemplate = command.WelcomeTemplate ?? current.WelcomeTemplate,
            GoodbyeTemplate = command.GoodbyeTemplate ?? current.GoodbyeTemplate,
            RulesText = command.RulesText ?? current.RulesText,
        };
        await store.UpdateChatSettingsAsync(command.ChatId, updated, command.ActorUserId, command.CorrelationId, ct);
        const string response = "✅ Lifecycle-настройки обновлены.";
        await store.EnqueueResponseAsync(command.ChatId, response, command.SourceMessageId, ct);
        return response;
    }
}
