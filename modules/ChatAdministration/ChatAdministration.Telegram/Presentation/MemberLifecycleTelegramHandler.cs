using ChatAdministration.Application.Services;
using ChatAdministration.Application.Commands;
using ChatAdministration.Domain.Models;
using BotFramework.Host.Composition.Builder;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using Microsoft.Extensions.Options;
using Telegram.Bot.Types.Enums;
using TelegramChatMemberUpdated = global::Telegram.Bot.Types.ChatMemberUpdated;

namespace ChatAdministration.Telegram.Presentation;

[ChatMember]
public sealed class MemberLifecycleTelegramHandler(
    MemberLifecycleService lifecycle,
    IOptions<BotFrameworkOptions> botOptions) : IUpdateHandler
{
    private readonly BotFrameworkOptions options = botOptions.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        var update = ctx.Update.ChatMember;
        if (update is null || update.NewChatMember.User.IsBot)
            return;

        var chatId = new ChatId(update.Chat.Id);
        var userId = new UserId(update.NewChatMember.User.Id);
        var displayName = DisplayName(update.NewChatMember.User.FirstName, update.NewChatMember.User.LastName, update.NewChatMember.User.Username, update.NewChatMember.User.Id);
        var now = DateTimeOffset.UtcNow;
        if (IsJoin(update))
        {
            await lifecycle.JoinedAsync(
                new MemberJoinedCommand(
                    $"member-joined:{ctx.Update.Id}:{update.Chat.Id}:{update.NewChatMember.User.Id}",
                    $"telegram-update:{options.TenantKey}:{ctx.Update.Id}",
                    $"member-joined:{update.Chat.Id}:{update.NewChatMember.User.Id}",
                    $"telegram-update:{ctx.Update.Id}",
                    chatId,
                    userId,
                    displayName,
                    update.NewChatMember.User.Username,
                    now),
                ctx.Ct);
        }
        else if (IsLeave(update))
        {
            await lifecycle.LeftAsync(
                new MemberLeftCommand(
                    $"member-left:{ctx.Update.Id}:{update.Chat.Id}:{update.NewChatMember.User.Id}",
                    $"telegram-update:{options.TenantKey}:{ctx.Update.Id}",
                    $"member-left:{update.Chat.Id}:{update.NewChatMember.User.Id}",
                    $"telegram-update:{ctx.Update.Id}",
                    chatId,
                    userId,
                    displayName,
                    update.NewChatMember.User.Username,
                    now),
                ctx.Ct);
        }
    }

    private static bool IsJoin(TelegramChatMemberUpdated update) =>
        update.NewChatMember.Status is ChatMemberStatus.Member or ChatMemberStatus.Restricted
        && update.OldChatMember.Status is ChatMemberStatus.Left or ChatMemberStatus.Kicked;

    private static bool IsLeave(TelegramChatMemberUpdated update) =>
        update.NewChatMember.Status is ChatMemberStatus.Left or ChatMemberStatus.Kicked
        && update.OldChatMember.Status is not (ChatMemberStatus.Left or ChatMemberStatus.Kicked);

    private static string DisplayName(string firstName, string? lastName, string? username, long id) =>
        string.Join(' ', new[] { firstName, lastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name
            ? name
            : username ?? $"User {id}";
}
