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

[Command("/cases")]
[Command("/case")]
[Command("/revoke")]
public sealed class CaseTelegramHandler(
    CaseService service,
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

        var chatId = new DomainChatId(message.Chat.Id);
        var actor = new UserId(message.From.Id);
        var target = await targetResolver.ResolveAsync(
            chatId,
            TelegramTargetReferenceParser.FromMessage(message),
            ctx.Ct);
        var actorRole = await ObserveRoleAsync(ctx.Bot, message.Chat.Id, message.From.Id, ctx.Ct);
        var targetRole = target is null
            ? actorRole
            : await ObserveRoleAsync(ctx.Bot, message.Chat.Id, target.UserId.Value, ctx.Ct);
        UserId? targetId = target?.UserId;
        var targetName = target?.DisplayName ?? DisplayName(message.From);

        if (message.Text.StartsWith("/cases", StringComparison.OrdinalIgnoreCase))
        {
            var result = await service.ListAsync(
                new ModerationCaseQuery(
                    chatId,
                    actor,
                    targetId,
                    actorRole,
                    targetRole,
                    DisplayName(message.From),
                    targetName,
                    20),
                ctx.Ct);
            await store.EnqueueResponseAsync(chatId, result.ResponseText, message.MessageId, ctx.Ct);
            return;
        }

        if (!CaseCommandParser.TryParseId(message.Text, out var caseId, out var parseError))
        {
            await store.EnqueueResponseAsync(chatId, parseError, message.MessageId, ctx.Ct);
            return;
        }

        var moderationCase = await store.LoadCaseAsync(chatId, caseId, ctx.Ct);
        if (moderationCase is null)
        {
            await store.EnqueueResponseAsync(chatId, "ℹ️ Case не найден.", message.MessageId, ctx.Ct);
            return;
        }

        if (message.Text.StartsWith("/case", StringComparison.OrdinalIgnoreCase))
        {
            var result = await service.ListAsync(
                new ModerationCaseQuery(
                    chatId,
                    actor,
                    moderationCase.TargetUserId,
                    actorRole,
                    await ObserveRoleAsync(ctx.Bot, message.Chat.Id, moderationCase.TargetUserId.Value, ctx.Ct),
                    DisplayName(message.From),
                    $"user {moderationCase.TargetUserId}",
                    100),
                ctx.Ct);
            var selected = result.Cases.FirstOrDefault(item => item.Id == caseId);
            await store.EnqueueResponseAsync(
                chatId,
                !result.Accepted ? result.ResponseText : selected is null ? "ℹ️ Case не найден." : Render(selected),
                message.MessageId,
                ctx.Ct);
            return;
        }

        var commandId = ctx.Update.Id != 0
            ? $"telegram-update:{ctx.Update.Id}"
            : $"telegram-case-revoke:{chatId}:{caseId}";
        var resultRevoke = await service.RevokeAsync(
            new RevokeModerationCaseCommand(
                commandId,
                $"telegram-update:{options.TenantKey}:{ctx.Update.Id}",
                $"case-revoke:{chatId}:{caseId}:{commandId}",
                $"telegram-update:{ctx.Update.Id}",
                chatId,
                actor,
                caseId,
                message.MessageId,
                DateTimeOffset.UtcNow),
            actorRole,
            await ObserveRoleAsync(ctx.Bot, message.Chat.Id, moderationCase.TargetUserId.Value, ctx.Ct),
            DisplayName(message.From),
            $"user {moderationCase.TargetUserId}",
            ctx.Ct);
        if (resultRevoke.Duplicate)
            await store.EnqueueResponseAsync(chatId, resultRevoke.ResponseText, message.MessageId, ctx.Ct);
    }

    private static string Render(ModerationCaseState item) =>
        $"🗂 Case <code>{item.Id}</code>\nAction: {item.Action}\nTarget: <code>{item.TargetUserId}</code>\nStatus: {item.Status}\nReason: {item.Reason ?? "без причины"}";

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
            return ChatMemberRole.Member;
        }
    }

    private static string DisplayName(User user) =>
        string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name
            ? name
            : user.Username ?? $"User {user.Id}";
}
