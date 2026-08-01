using System.Globalization;
using ChatAdministration.Application.Commands;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;
using BotFramework.Host.Composition.Builder;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using DomainChatId = ChatAdministration.Domain.Models.ChatId;

namespace ChatAdministration.Telegram.Presentation;

[Command("/role")]
[Command("/roles")]
public sealed class RoleTelegramHandler(
    RoleService service,
    CustomRoleService customRoles,
    IChatAdministrationStore store,
    ITargetResolver targetResolver,
    IOptions<BotFrameworkOptions> botOptions) : IUpdateHandler
{
    private readonly BotFrameworkOptions options = botOptions.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        var message = ctx.Update.Message;
        if (message?.Text is null || message.From is null)
            return;
        var tokens = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = tokens[0].Split('@', 2)[0].ToLowerInvariant();
        if (command == "/role" && tokens.Length >= 2
            && (string.Equals(tokens[1], "define", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tokens[1], "delete", StringComparison.OrdinalIgnoreCase)))
        {
            await HandleCustomRoleCommandAsync(ctx, message, tokens, customRoles, store, options);
            return;
        }
        var target = await targetResolver.ResolveAsync(
            new DomainChatId(message.Chat.Id),
            TelegramTargetReferenceParser.FromMessage(message, allowActor: true),
            ctx.Ct);
        if (target is null)
            return;

        var actorRole = await TelegramRoleResolver.ResolveAsync(ctx.Bot, options, message.Chat.Id, message.From.Id, ctx.Ct);
        var targetRole = await TelegramRoleResolver.ResolveAsync(ctx.Bot, options, message.Chat.Id, target.UserId.Value, ctx.Ct);
        var chatId = new DomainChatId(message.Chat.Id);
        var actorId = new UserId(message.From.Id);
        var targetId = target.UserId;
        var actorName = DisplayName(message.From);
        var targetName = target.DisplayName;
        if (command == "/roles")
        {
            var response = await service.ListAsync(
                chatId,
                actorId,
                targetId,
                actorRole,
                targetRole,
                actorName,
                targetName,
                ctx.Ct);
            await store.EnqueueResponseAsync(chatId, response, message.MessageId, ctx.Ct);
            return;
        }

        var roleToken = tokens.Length == 2 ? tokens[1] : tokens.ElementAtOrDefault(2);
        var assign = !string.Equals(tokens.ElementAtOrDefault(1), "remove", StringComparison.OrdinalIgnoreCase);
        if (!assign && tokens.Length != 3)
        {
            await store.EnqueueResponseAsync(chatId, "Использование: /role [remove] <helper|trusted|moderator|admin> ответом на сообщение.", message.MessageId, ctx.Ct);
            return;
        }
        var customRoleId = roleToken?.StartsWith("custom:", StringComparison.OrdinalIgnoreCase) == true
            ? new RoleId(roleToken[7..])
            : (RoleId?)null;
        var builtInRole = Enum.TryParse<ChatMemberRole>(roleToken, true, out var parsedRole)
            ? parsedRole
            : ChatMemberRole.Member;
        if (customRoleId is { Value.Length: 0 }
            || customRoleId is null && builtInRole == ChatMemberRole.Member
            || customRoleId is null && string.Equals(roleToken, nameof(ChatMemberRole.Member), StringComparison.OrdinalIgnoreCase))
        {
            await store.EnqueueResponseAsync(chatId, "Неизвестная роль. Доступно: helper, trusted, moderator, admin или custom:<id>.", message.MessageId, ctx.Ct);
            return;
        }

        if (customRoleId is not null)
            builtInRole = ChatMemberRole.Member;

        var roleLabel = customRoleId is { } custom
            ? $"custom:{custom.Value}"
            : builtInRole.ToString();
        var roleEvent = customRoleId is { } customId
            ? assign
                ? (IDomainEvent)new CustomRoleAssigned(chatId, targetId, customId)
                : new CustomRoleRemoved(chatId, targetId, customId)
            : new MemberRoleAssigned(chatId, targetId, builtInRole);

        var mutation = new RoleMutationCommand(
            $"role:{message.Chat.Id}:{message.MessageId}:{ctx.Update.Id}",
            $"telegram-update:{options.TenantKey}:{ctx.Update.Id}",
            $"role:{message.Chat.Id}:{message.MessageId}",
            $"telegram-update:{ctx.Update.Id}",
            chatId,
            actorId,
            targetId,
            builtInRole,
            assign,
            new MemberState
            {
                ChatId = chatId,
                UserId = targetId,
                DisplayName = targetName,
                Roles = new HashSet<ChatMemberRole> { targetRole },
            },
            roleEvent,
            assign ? $"✅ Роль {roleLabel} назначена." : $"✅ Роль {roleLabel} снята.",
            DateTimeOffset.UtcNow,
            message.MessageId);
        mutation = mutation with { CustomRoleId = customRoleId };
        await service.ChangeAsync(mutation, actorRole, targetRole, actorName, targetName, ctx.Ct);
    }

    private static async Task HandleCustomRoleCommandAsync(
        UpdateContext ctx,
        Message message,
        IReadOnlyList<string> tokens,
        CustomRoleService customRoles,
        IChatAdministrationStore store,
        BotFrameworkOptions options)
    {
        var remove = string.Equals(tokens[1], "delete", StringComparison.OrdinalIgnoreCase);
        var validShape = remove ? tokens.Count == 3 : tokens.Count == 5;
        if (!validShape || !int.TryParse(remove ? "0" : tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var rank))
        {
            await store.EnqueueResponseAsync(new DomainChatId(message.Chat.Id), remove
                ? "Использование: /role delete <id>"
                : "Использование: /role define <id> <rank> <Permission1,Permission2>", message.MessageId, ctx.Ct);
            return;
        }

        var permissions = new HashSet<Permission>();
        if (!remove)
        {
            foreach (var permissionToken in tokens[4].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!Enum.TryParse(permissionToken, true, out Permission permission))
                {
                    await store.EnqueueResponseAsync(
                        new DomainChatId(message.Chat.Id),
                        $"Неизвестное permission: {permissionToken}",
                        message.MessageId,
                        ctx.Ct);
                    return;
                }

                permissions.Add(permission);
            }
        }

        await customRoles.ExecuteAsync(
            new CustomRoleMutationCommand(
                $"custom-role:{ctx.Update.Id}:{message.Chat.Id}:{message.MessageId}",
                $"telegram-update:{ctx.Update.Id}",
                $"custom-role:{message.Chat.Id}:{message.MessageId}",
                new DomainChatId(message.Chat.Id),
                new UserId(message.From!.Id),
                new RoleId(tokens[2].Trim().ToLowerInvariant()),
                tokens[2],
                rank,
                permissions,
                remove,
                await TelegramRoleResolver.ResolveAsync(ctx.Bot, options, message.Chat.Id, message.From.Id, ctx.Ct),
                DisplayName(message.From),
                DateTimeOffset.UtcNow,
                message.MessageId),
            ctx.Ct);
    }

    private static string DisplayName(User user) =>
        string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name ? name : user.Username ?? $"User {user.Id}";
}
