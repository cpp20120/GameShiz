using ChatAdministration.Application.Commands;
using ChatAdministration.Application.Parsing;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using BotFramework.Host.Composition.Builder;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using DomainChatId = ChatAdministration.Domain.Models.ChatId;

namespace ChatAdministration.Telegram.Presentation;

[Command("/mute")]
public sealed class ModerationTelegramHandler(
    ModerationCommandService service,
    IChatAdministrationStore store,
    ITargetResolver targetResolver,
    IOptions<BotFrameworkOptions> botOptions,
    ILogger<ModerationTelegramHandler> logger) : IUpdateHandler
{
    private readonly BotFrameworkOptions options = botOptions.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        var message = ctx.Update.Message;
        if (message?.Text is null || message.From is null)
            return;

        if (!MuteCommandParser.TryParse(message.Text, out var parsed, out var parseError))
        {
            await store.EnqueueResponseAsync(new DomainChatId(message.Chat.Id), parseError!, message.MessageId, ctx.Ct);
            return;
        }

        var target = await targetResolver.ResolveAsync(
            new DomainChatId(message.Chat.Id),
            TelegramTargetReferenceParser.FromMessage(message),
            ctx.Ct);
        if (target is null || target.UserId == new UserId(0))
        {
            await store.EnqueueResponseAsync(
                new DomainChatId(message.Chat.Id),
                "Ответьте командой /mute на сообщение пользователя.",
                message.MessageId,
                ctx.Ct);
            return;
        }

        var actorRole = await ObserveRoleAsync(ctx.Bot, message.Chat.Id, message.From.Id, ctx.Ct);
        var targetRole = await ObserveRoleAsync(ctx.Bot, message.Chat.Id, target.UserId.Value, ctx.Ct);
        var commandId = BuildCommandId(message, ctx.Update.Id);
        var correlationId = $"moderation:{message.Chat.Id}:{commandId}";
        var command = new MuteMemberCommand(
            commandId,
            $"telegram-update:{options.TenantKey}:{ctx.Update.Id}",
            correlationId,
            $"telegram-update:{ctx.Update.Id}",
            new DomainChatId(message.Chat.Id),
            new UserId(message.From.Id),
            target.UserId,
            DisplayName(message.From),
            target.DisplayName,
            parsed!.Duration,
            parsed.Reason,
            DateTimeOffset.UtcNow,
            message.MessageId,
            actorRole,
            targetRole);

        var result = await service.ExecuteMuteAsync(command, ctx.Ct);
        if (result.Duplicate)
            logger.LogDebug("Duplicate moderation command ignored: {CommandId}", commandId);
    }

    private static async Task<ChatMemberRole> ObserveRoleAsync(
        ITelegramBotClient bot,
        long chatId,
        long userId,
        CancellationToken ct)
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
            // The application keeps internal roles. A failed observation must
            // never accidentally elevate an actor, so it is treated as Member.
            return ChatMemberRole.Member;
        }
    }

    private static string BuildCommandId(Message message, int updateId) =>
        updateId != 0
            ? $"telegram-update:{updateId}"
            : $"telegram-message:{message.Chat.Id}:{message.MessageId}";

    private static string DisplayName(User user) =>
        string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name
            ? name
            : user.Username ?? $"User {user.Id}";
}
