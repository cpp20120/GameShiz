using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
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

[Command("/warnings")]
[Command("/unwarn")]
[Command("/clearwarnings")]
public sealed class WarningTelegramHandler(
    WarningService service,
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

        var command = message.Text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0]
            .Split('@', 2)[0]
            .ToLowerInvariant();
        var target = await targetResolver.ResolveAsync(
            new DomainChatId(message.Chat.Id),
            TelegramTargetReferenceParser.FromMessage(message, allowActor: command == "/warnings"),
            ctx.Ct);
        if (target is null)
        {
            await store.EnqueueResponseAsync(
                new DomainChatId(message.Chat.Id),
                "Ответьте командой на сообщение пользователя.",
                message.MessageId,
                ctx.Ct);
            return;
        }

        var chatId = new DomainChatId(message.Chat.Id);
        var actorRole = await ObserveRoleAsync(ctx.Bot, message.Chat.Id, message.From.Id, ctx.Ct);
        var targetRole = await ObserveRoleAsync(ctx.Bot, message.Chat.Id, target.UserId.Value, ctx.Ct);
        var actorName = DisplayName(message.From);
        var targetName = target.DisplayName;
        if (command == "/warnings")
        {
            var result = await service.ListAsync(
                chatId,
                new UserId(message.From.Id),
                target.UserId,
                actorRole,
                targetRole,
                actorName,
                targetName,
                activeOnly: true,
                ctx.Ct);
            await store.EnqueueResponseAsync(chatId, result.ResponseText, message.MessageId, ctx.Ct);
            return;
        }

        WarningId? warningId = null;
        if (command == "/unwarn")
        {
            var argument = message.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
            if (argument is null || !Guid.TryParse(argument, out var parsedId))
            {
                await store.EnqueueResponseAsync(chatId, "Использование: /unwarn <warning-id> ответом на сообщение.", message.MessageId, ctx.Ct);
                return;
            }
            warningId = new WarningId(parsedId);
        }

        var commandId = $"warning:{ctx.Update.Id}:{message.Chat.Id}:{message.MessageId}";
        await service.RevokeAsync(
            new WarningMutationCommand(
                commandId,
                $"telegram-update:{options.TenantKey}:{ctx.Update.Id}",
                $"warning:{message.Chat.Id}:{message.MessageId}",
                $"telegram-update:{ctx.Update.Id}",
                chatId,
                new UserId(message.From.Id),
                target.UserId,
                [],
                [],
                command == "/clearwarnings" ? "✅ Все предупреждения сняты." : "✅ Предупреждение снято.",
                DateTimeOffset.UtcNow,
                message.MessageId),
            actorRole,
            targetRole,
            actorName,
            targetName,
            warningId,
            command == "/clearwarnings" ? WarningRevocationReason.Cleared : WarningRevocationReason.Manual,
            ctx.Ct);
    }

    private static async Task<ChatMemberRole> ObserveRoleAsync(ITelegramBotClient bot, long chatId, long userId, CancellationToken ct)
    {
        try
        {
            var member = await bot.GetChatMember(chatId, userId, ct);
            return member.Status switch
            {
                ChatMemberStatus.Creator => ChatMemberRole.Owner,
                ChatMemberStatus.Administrator => ChatMemberRole.Admin,
                _ => ChatMemberRole.Member,
            };
        }
        catch (ApiRequestException)
        {
            return ChatMemberRole.Member;
        }
    }

    private static string DisplayName(User user) =>
        string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name ? name : user.Username ?? $"User {user.Id}";
}
