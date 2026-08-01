using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using BotFramework.Host.Composition.Builder;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using Microsoft.Extensions.Options;
using global::Telegram.Bot;
using TelegramBotClient = global::Telegram.Bot.ITelegramBotClient;
using TelegramMessage = global::Telegram.Bot.Types.Message;
using TelegramUser = global::Telegram.Bot.Types.User;
using DomainEntityType = ChatAdministration.Domain.Models.MessageEntityType;
using DomainMessageEntity = ChatAdministration.Domain.Models.MessageEntity;
using TelegramMessageEntity = global::Telegram.Bot.Types.MessageEntity;
using DomainChatId = ChatAdministration.Domain.Models.ChatId;

namespace ChatAdministration.Telegram.Presentation;

[Message]
public sealed class AutomoderationTelegramHandler(
    ModerationCommandService service,
    IOptions<BotFrameworkOptions> botOptions) : IUpdateHandler
{
    private readonly BotFrameworkOptions options = botOptions.Value;

    public async Task HandleAsync(UpdateContext ctx)
    {
        var message = ctx.Update.Message;
        if (message?.From is null || message.From.IsBot)
            return;

        // Private messages are the admin control channel, not a moderation
        // tenant. Running automod there can create delete cases for messages
        // that Telegram does not allow the bot to delete.
        if (message.Chat.Type == global::Telegram.Bot.Types.Enums.ChatType.Private)
            return;

        var normalized = Normalize(message);
        var observedRole = await TelegramRoleResolver.ResolveAsync(ctx.Bot, options, message.Chat.Id, message.From.Id, ctx.Ct);
        await service.ExecuteAutomaticAsync(
            normalized,
            observedRole,
            DisplayName(message.From),
            message.From.Username,
            ctx.Ct);
    }

    private static NormalizedMessage Normalize(TelegramMessage message) => new()
    {
        ChatId = new DomainChatId(message.Chat.Id),
        MessageId = message.MessageId,
        AuthorId = new UserId(message.From!.Id),
        Text = message.Text ?? message.Caption,
        Entities = (message.Entities ?? message.CaptionEntities ?? [])
            .Select(MapEntity)
            .ToArray(),
        ContentType = ContentTypeOf(message),
        IsForwarded = message.ForwardOrigin is not null,
        IsServiceMessage = message.NewChatMembers is not null || message.LeftChatMember is not null,
        SentAt = message.Date.ToUniversalTime(),
    };

    private static MessageContentType ContentTypeOf(TelegramMessage message) =>
        message.Photo is not null ? MessageContentType.Photo
        : message.Video is not null ? MessageContentType.Video
        : message.Sticker is not null ? MessageContentType.Sticker
        : message.Animation is not null ? MessageContentType.Gif
        : message.Voice is not null ? MessageContentType.Voice
        : message.Document is not null ? MessageContentType.Document
        : message.Contact is not null ? MessageContentType.Contact
        : message.Location is not null ? MessageContentType.Location
        : MessageContentType.Text;

    private static DomainMessageEntity MapEntity(TelegramMessageEntity entity) => new(
        entity.Type switch
        {
            global::Telegram.Bot.Types.Enums.MessageEntityType.Url => DomainEntityType.Url,
            global::Telegram.Bot.Types.Enums.MessageEntityType.TextLink => DomainEntityType.TextLink,
            global::Telegram.Bot.Types.Enums.MessageEntityType.TextMention => DomainEntityType.TextMention,
            global::Telegram.Bot.Types.Enums.MessageEntityType.Mention => DomainEntityType.Mention,
            global::Telegram.Bot.Types.Enums.MessageEntityType.BotCommand => DomainEntityType.BotCommand,
            _ => DomainEntityType.Mention,
        },
        entity.Offset,
        entity.Length,
        entity.Url);

    private static string DisplayName(TelegramUser user) =>
        string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name
            ? name
            : user.Username ?? $"User {user.Id}";
}
