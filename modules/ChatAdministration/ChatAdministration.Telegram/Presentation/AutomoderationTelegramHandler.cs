using ChatAdministration.Application.Services;
using ChatAdministration.Domain.Models;
using BotFramework.Sdk.UpdateHandling;
using BotFramework.Sdk.UpdateHandling.Routes;
using global::Telegram.Bot;
using TelegramBotClient = global::Telegram.Bot.ITelegramBotClient;
using TelegramApiException = global::Telegram.Bot.Exceptions.ApiRequestException;
using TelegramMessage = global::Telegram.Bot.Types.Message;
using TelegramUser = global::Telegram.Bot.Types.User;
using TelegramChatMemberStatus = global::Telegram.Bot.Types.Enums.ChatMemberStatus;
using DomainEntityType = ChatAdministration.Domain.Models.MessageEntityType;
using DomainMessageEntity = ChatAdministration.Domain.Models.MessageEntity;
using TelegramMessageEntity = global::Telegram.Bot.Types.MessageEntity;
using DomainChatId = ChatAdministration.Domain.Models.ChatId;

namespace ChatAdministration.Telegram.Presentation;

[Message]
public sealed class AutomoderationTelegramHandler(ModerationCommandService service) : IUpdateHandler
{
    public async Task HandleAsync(UpdateContext ctx)
    {
        var message = ctx.Update.Message;
        if (message?.From is null || message.From.IsBot)
            return;

        var normalized = Normalize(message);
        var observedRole = await ObserveRoleAsync(ctx.Bot, message.Chat.Id, message.From.Id, ctx.Ct);
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

    private static async Task<ChatMemberRole> ObserveRoleAsync(
        TelegramBotClient bot,
        long chatId,
        long userId,
        CancellationToken ct)
    {
        try
        {
            var member = await bot.GetChatMember(chatId, userId, ct);
            return member.Status switch
            {
                TelegramChatMemberStatus.Creator => ChatMemberRole.Owner,
                TelegramChatMemberStatus.Administrator => ChatMemberRole.Admin,
                _ => ChatMemberRole.Member,
            };
        }
        catch (TelegramApiException)
        {
            return ChatMemberRole.Member;
        }
    }

    private static string DisplayName(TelegramUser user) =>
        string.Join(' ', new[] { user.FirstName, user.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)))
        is { Length: > 0 } name
            ? name
            : user.Username ?? $"User {user.Id}";
}
