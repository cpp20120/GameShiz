using ChatAdministration.Application.Commands;
using ChatAdministration.Application.Parsing;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using BotFramework.Host.Composition.Builder;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;
using DomainChatId = ChatAdministration.Domain.Models.ChatId;

namespace ChatAdministration.Telegram.Presentation;

[Command("/purge")]
public sealed class PurgeTelegramHandler(
    PurgeService service,
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
        if (!PurgeCommandParser.TryParse(message.Text, out var count, out var error))
        {
            await store.EnqueueResponseAsync(new DomainChatId(message.Chat.Id), error!, message.MessageId, ctx.Ct);
            return;
        }

        var actorRole = await TelegramRoleResolver.ResolveAsync(ctx.Bot, options, message.Chat.Id, message.From.Id, ctx.Ct);
        var target = await targetResolver.ResolveAsync(
            new DomainChatId(message.Chat.Id),
            TelegramTargetReferenceParser.FromMessage(message),
            ctx.Ct);
        var command = new PurgeMessagesCommand(
            $"purge:{message.Chat.Id}:{message.MessageId}:{ctx.Update.Id}",
            $"telegram-update:{options.TenantKey}:{ctx.Update.Id}",
            $"purge:{message.Chat.Id}:{message.MessageId}",
            $"telegram-update:{ctx.Update.Id}",
            new DomainChatId(message.Chat.Id),
            new UserId(message.From.Id),
            target?.UserId,
            count,
            message.MessageId,
            DateTimeOffset.UtcNow,
            actorRole);
        await service.ExecuteAsync(command, ctx.Ct);
    }

}
