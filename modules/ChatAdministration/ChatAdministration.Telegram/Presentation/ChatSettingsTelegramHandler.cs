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

[Command("/settings")]
public sealed class ChatSettingsTelegramHandler(
    ChatSettingsService service,
    IChatAdministrationStore store,
    IOptions<BotFrameworkOptions> botOptions) : IUpdateHandler
{
    private readonly BotFrameworkOptions options = botOptions.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        var message = ctx.Update.Message;
        if (message?.Text is null || message.From is null)
            return;
        if (!ChatSettingsCommandParser.TryParse(message.Text, out var key, out var value, out var error))
        {
            await store.EnqueueResponseAsync(new DomainChatId(message.Chat.Id), error!, message.MessageId, ctx.Ct);
            return;
        }

        var actorRole = await ObserveRoleAsync(ctx.Bot, message.Chat.Id, message.From.Id, ctx.Ct);
        var command = new ChatSettingsCommand(
            $"settings:{ctx.Update.Id}:{message.Chat.Id}:{message.MessageId}",
            $"telegram-update:{options.TenantKey}:{ctx.Update.Id}",
            $"settings:{message.Chat.Id}:{message.MessageId}",
            new DomainChatId(message.Chat.Id),
            new UserId(message.From.Id),
            key,
            value,
            message.MessageId,
            DateTimeOffset.UtcNow,
            actorRole,
            DisplayName(message.From.FirstName, message.From.LastName, message.From.Username, message.From.Id));
        await service.ExecuteAsync(command, ctx.Ct);
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
            return ChatMemberRole.Member;
        }
    }

    private static string DisplayName(string firstName, string? lastName, string? username, long id) =>
        string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name
            ? name
            : username ?? $"User {id}";
}
