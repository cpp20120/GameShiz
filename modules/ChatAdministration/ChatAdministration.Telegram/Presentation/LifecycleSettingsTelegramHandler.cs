using ChatAdministration.Application.Commands;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using BotFramework.Host.Composition.Builder;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types.Enums;

namespace ChatAdministration.Telegram.Presentation;

[Command("/welcome")]
[Command("/goodbye")]
[Command("/setwelcome")]
[Command("/setgoodbye")]
[Command("/setrules")]
public sealed class LifecycleSettingsTelegramHandler(
    LifecycleSettingsService service,
    IChatAdministrationStore store,
    IOptions<BotFrameworkOptions> botOptions) : IUpdateHandler
{
    private readonly BotFrameworkOptions options = botOptions.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        var message = ctx.Update.Message;
        if (message?.Text is null || message.From is null)
            return;

        var tokens = message.Text.Split(' ', 2, StringSplitOptions.TrimEntries);
        var command = tokens[0].Split('@', 2)[0].ToLowerInvariant();
        var value = tokens.Length == 2 ? tokens[1].Trim() : null;
        var actorRole = await ObserveRoleAsync(ctx.Bot, message.Chat.Id, message.From.Id, ctx.Ct);
        if (command is "/welcome" or "/goodbye")
        {
            if (value is not ("on" or "off"))
            {
                await store.EnqueueResponseAsync(new ChatId(message.Chat.Id), "Использование: /welcome on|off или /goodbye on|off.", message.MessageId, ctx.Ct);
                return;
            }
        }
        else if (string.IsNullOrWhiteSpace(value))
        {
            await store.EnqueueResponseAsync(new ChatId(message.Chat.Id), "Укажите текст после команды.", message.MessageId, ctx.Ct);
            return;
        }

        var update = command switch
        {
            "/welcome" => CreateCommand(ctx, message, actorRole, null, null, null, value == "on", null),
            "/goodbye" => CreateCommand(ctx, message, actorRole, null, null, null, null, value == "on"),
            "/setwelcome" => CreateCommand(ctx, message, actorRole, value, null, null, null, null),
            "/setgoodbye" => CreateCommand(ctx, message, actorRole, null, value, null, null, null),
            "/setrules" => CreateCommand(ctx, message, actorRole, null, null, value, null, null),
            _ => throw new InvalidOperationException($"Unsupported lifecycle command '{command}'."),
        };
        await service.ExecuteAsync(update, ctx.Ct);
    }

    private UpdateLifecycleSettingsCommand CreateCommand(
        UpdateContext ctx,
        global::Telegram.Bot.Types.Message message,
        ChatMemberRole actorRole,
        string? welcomeTemplate,
        string? goodbyeTemplate,
        string? rulesText,
        bool? welcomeEnabled,
        bool? goodbyeEnabled)
    {
        return new UpdateLifecycleSettingsCommand(
            $"lifecycle-settings:{ctx.Update.Id}:{message.Chat.Id}:{message.MessageId}",
            $"telegram-update:{options.TenantKey}:{ctx.Update.Id}",
            $"lifecycle-settings:{message.Chat.Id}:{message.MessageId}",
            new ChatId(message.Chat.Id),
            new UserId(message.From!.Id),
            welcomeTemplate,
            goodbyeTemplate,
            rulesText,
            welcomeEnabled,
            goodbyeEnabled,
            DateTimeOffset.UtcNow,
            actorRole,
            DisplayName(message.From.FirstName, message.From.LastName, message.From.Username, message.From.Id));
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

    private static string DisplayName(string firstName, string? lastName, string? username, long id) =>
        string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name
            ? name
            : username ?? $"User {id}";
}
