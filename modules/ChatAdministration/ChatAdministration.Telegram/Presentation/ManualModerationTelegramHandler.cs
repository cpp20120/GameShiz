using ChatAdministration.Application.Commands;
using ChatAdministration.Application.Parsing;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using BotFramework.Host.Composition.Builder;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using DomainChatId = ChatAdministration.Domain.Models.ChatId;

namespace ChatAdministration.Telegram.Presentation;

[Command("/warn")]
[Command("/unmute")]
[Command("/ban")]
[Command("/unban")]
[Command("/kick")]
public sealed class ManualModerationTelegramHandler(
    ModerationCommandService service,
    IChatAdministrationStore store,
    ITargetResolver targetResolver,
    IOptions<BotFrameworkOptions> botOptions,
    ILogger<ManualModerationTelegramHandler> logger) : IUpdateHandler
{
    private readonly BotFrameworkOptions options = botOptions.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        var message = ctx.Update.Message;
        if (message?.Text is null || message.From is null)
            return;

        var action = ParseAction(message.Text);
        if (action is null)
            return;

        if (!ManualModerationCommandParser.TryParse(message.Text, action.Value, out var parsed, out var parseError))
        {
            await store.EnqueueResponseAsync(new DomainChatId(message.Chat.Id), parseError!, message.MessageId, ctx.Ct);
            return;
        }

        var targetReference = TelegramTargetReferenceParser.FromMessage(message);
        var target = await targetResolver.ResolveAsync(
            new DomainChatId(message.Chat.Id),
            targetReference,
            ctx.Ct);
        if (target is null || target.UserId == new UserId(0))
        {
            await store.EnqueueResponseAsync(
                new DomainChatId(message.Chat.Id),
                targetReference is null
                    ? $"Ответьте командой /{action.Value.ToString().ToLowerInvariant()} на сообщение пользователя или укажите @username."
                    : "Не удалось определить пользователя. Ответьте командой на сообщение, которое ещё доступно боту, или укажите сохранённый @username.",
                message.MessageId,
                ctx.Ct);
            return;
        }

        var actorRole = await TelegramRoleResolver.ResolveAsync(ctx.Bot, options, message.Chat.Id, message.From.Id, ctx.Ct);
        var targetRole = await TelegramRoleResolver.ResolveAsync(ctx.Bot, options, message.Chat.Id, target.UserId.Value, ctx.Ct);
        var commandId = BuildCommandId(message, ctx.Update.Id);
        var correlationId = $"moderation:{message.Chat.Id}:{commandId}";
        var command = new ManualModerationCommand(
            commandId,
            $"telegram-update:{options.TenantKey}:{ctx.Update.Id}",
            correlationId,
            $"telegram-update:{ctx.Update.Id}",
            new DomainChatId(message.Chat.Id),
            new UserId(message.From.Id),
            target.UserId,
            action.Value,
            parsed!.Duration,
            parsed.Reason,
            DateTimeOffset.UtcNow,
            message.MessageId,
            actorRole,
            targetRole,
            DisplayName(message.From),
            target.DisplayName);

        var result = await service.ExecuteManualAsync(command, ctx.Ct);
        if (result.Duplicate)
            logger.LogDebug("Duplicate moderation command ignored: {CommandId}", commandId);
    }

    private static ModerationAction? ParseAction(string text)
    {
        var command = text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries)[0]
            .Split('@', 2)[0]
            .ToLowerInvariant();
        return command switch
        {
            "/warn" => ModerationAction.Warn,
            "/unmute" => ModerationAction.Unmute,
            "/ban" => ModerationAction.Ban,
            "/unban" => ModerationAction.Unban,
            "/kick" => ModerationAction.Kick,
            _ => null,
        };
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
