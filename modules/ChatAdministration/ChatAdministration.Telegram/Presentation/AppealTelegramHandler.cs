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
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using DomainChatId = ChatAdministration.Domain.Models.ChatId;

namespace ChatAdministration.Telegram.Presentation;

[Command("/appeal")]
[Command("/approveappeal")]
[Command("/rejectappeal")]
public sealed class AppealTelegramHandler(
    AppealService service,
    IChatAdministrationStore store,
    IOptions<BotFrameworkOptions> botOptions) : IUpdateHandler
{
    private readonly BotFrameworkOptions options = botOptions.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        var message = ctx.Update.Message;
        if (message?.Text is null || message.From is null)
            return;

        var chatId = new DomainChatId(message.Chat.Id);
        var actor = new UserId(message.From.Id);
        var commandId = ctx.Update.Id != 0
            ? $"telegram-update:{ctx.Update.Id}"
            : $"telegram-appeal:{chatId}:{message.MessageId}";
        if (message.Text.StartsWith("/appeal", StringComparison.OrdinalIgnoreCase))
        {
            if (!AppealCommandParser.TryParseOpen(message.Text, out var caseId, out var appealText, out var parseError))
            {
                await store.EnqueueResponseAsync(chatId, parseError, message.MessageId, ctx.Ct);
                return;
            }

            await service.OpenAsync(
                new OpenAppealCommand(
                    commandId,
                    $"telegram-update:{options.TenantKey}:{ctx.Update.Id}",
                    $"appeal:{chatId}:{caseId}:{commandId}",
                    $"telegram-update:{ctx.Update.Id}",
                    chatId,
                    actor,
                    caseId,
                    appealText,
                    message.MessageId,
                    DateTimeOffset.UtcNow),
                DisplayName(message.From),
                ctx.Ct);
            return;
        }

        if (!AppealCommandParser.TryParseResolution(message.Text, out var appealId, out var comment, out var resolutionError))
        {
            await store.EnqueueResponseAsync(chatId, resolutionError, message.MessageId, ctx.Ct);
            return;
        }
        var appeal = await store.LoadAppealAsync(chatId, appealId, ctx.Ct);
        var moderationCase = appeal is null ? null : await store.LoadCaseAsync(chatId, appeal.CaseId, ctx.Ct);
        if (appeal is null || moderationCase is null)
        {
            await store.EnqueueResponseAsync(chatId, "ℹ️ Appeal или связанный case не найден.", message.MessageId, ctx.Ct);
            return;
        }

        var approve = message.Text.StartsWith("/approveappeal", StringComparison.OrdinalIgnoreCase);
        await service.ResolveAsync(
            new ResolveAppealCommand(
                commandId,
                $"telegram-update:{options.TenantKey}:{ctx.Update.Id}",
                $"appeal-resolution:{chatId}:{appealId}:{commandId}",
                $"telegram-update:{ctx.Update.Id}",
                chatId,
                actor,
                appealId,
                approve,
                comment,
                message.MessageId,
                DateTimeOffset.UtcNow),
            await TelegramRoleResolver.ResolveAsync(ctx.Bot, options, message.Chat.Id, message.From.Id, ctx.Ct),
            await TelegramRoleResolver.ResolveAsync(ctx.Bot, options, message.Chat.Id, moderationCase.TargetUserId.Value, ctx.Ct),
            DisplayName(message.From),
            $"user {moderationCase.TargetUserId}",
            ctx.Ct);
    }

    private static string DisplayName(User user) =>
        string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name
            ? name
            : user.Username ?? $"User {user.Id}";
}
