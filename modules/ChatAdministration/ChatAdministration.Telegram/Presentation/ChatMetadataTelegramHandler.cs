using ChatAdministration.Application.Commands;
using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using Telegram.Bot.Types.Enums;

namespace ChatAdministration.Telegram.Presentation;

[Message]
[ChatMember]
[MyChatMember]
public sealed class ChatMetadataTelegramHandler(
    ChatMetadataService metadata) : IUpdateHandler
{
    public Task HandleAsync(UpdateContext ctx)
    {
        var chat = ctx.Update.Message?.Chat
            ?? ctx.Update.ChatMember?.Chat
            ?? ctx.Update.MyChatMember?.Chat;
        if (chat is null)
            return Task.CompletedTask;

        var title = chat.Title
            ?? chat.Username
            ?? ctx.Update.Message?.From?.FirstName
            ?? ctx.Update.MyChatMember?.From?.FirstName
            ?? "Telegram chat";
        return metadata.ObserveAsync(
            new ChatMetadataCommand(
                $"chat-metadata:{ctx.Update.Id}:{chat.Id}",
                $"telegram-update:{ctx.Update.Id}",
                $"telegram-update:{ctx.Update.Id}",
                new ChatId(chat.Id),
                MapType(chat.Type),
                title,
                DateTimeOffset.UtcNow),
            ctx.Ct);
    }

    private static ChatAdministration.Domain.Models.ChatType MapType(global::Telegram.Bot.Types.Enums.ChatType type) => type switch
    {
        global::Telegram.Bot.Types.Enums.ChatType.Private => ChatAdministration.Domain.Models.ChatType.Private,
        global::Telegram.Bot.Types.Enums.ChatType.Group => ChatAdministration.Domain.Models.ChatType.Group,
        global::Telegram.Bot.Types.Enums.ChatType.Channel => ChatAdministration.Domain.Models.ChatType.Channel,
        _ => ChatAdministration.Domain.Models.ChatType.Supergroup,
    };
}
