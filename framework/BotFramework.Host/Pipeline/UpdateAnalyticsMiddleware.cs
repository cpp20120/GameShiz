using BotFramework.Sdk;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace BotFramework.Host.Pipeline;

public sealed class UpdateAnalyticsMiddleware(IAnalyticsService analytics) : IUpdateMiddleware
{
    public async Task InvokeAsync(UpdateContext ctx, UpdateDelegate next)
    {
        Track(ctx.Update, ctx.UserId, ctx.ChatId);
        await next(ctx);
    }

    private void Track(Update update, long userId, long chatId)
    {
        var kind = Kind(update);
        var tags = new Dictionary<string, object?>
        {
            ["update_id"] = update.Id,
            ["user_id"] = userId,
            ["chat_id"] = chatId,
            ["kind"] = kind,
            ["chat_type"] = ChatType(update),
        };

        switch (update)
        {
            case { Message: { Text: { } text } }:
                if (TryCommandToken(text) is { } command)
                {
                    tags["command"] = command;
                    tags["has_args"] = HasArgs(text);
                    analytics.Track("telegram", "command", tags);
                    return;
                }

                tags["text_length"] = text.Length;
                analytics.Track("telegram", "message", tags);
                return;

            case { Message.Dice: { } dice }:
                tags["emoji"] = dice.Emoji;
                tags["value"] = dice.Value;
                analytics.Track("telegram", "dice", tags);
                return;

            case { CallbackQuery: { } callback }:
                var data = callback.Data ?? "";
                tags["callback_prefix"] = CallbackPrefix(data);
                tags["callback_length"] = data.Length;
                tags["message_chat_id"] = callback.Message?.Chat.Id;
                analytics.Track("telegram", "callback", tags);
                return;

            default:
                analytics.Track("telegram", "update", tags);
                return;
        }
    }

    private static string Kind(Update update) => update switch
    {
        { Message.Text: { } } => "text",
        { Message.Dice: { } } => "dice",
        { CallbackQuery: { } } => "callback",
        { ChannelPost: { } } => "channel_post",
        { EditedMessage: { } } => "edited_message",
        { InlineQuery: { } } => "inline_query",
        _ => update.Type.ToString().ToLowerInvariant(),
    };

    private static string? ChatType(Update update)
    {
        var chat = update.Message?.Chat
            ?? update.EditedMessage?.Chat
            ?? update.ChannelPost?.Chat
            ?? update.CallbackQuery?.Message?.Chat;
        return chat?.Type.ToString().ToLowerInvariant();
    }

    private static string? TryCommandToken(string text)
    {
        var span = text.AsSpan().TrimStart();
        if (span.IsEmpty || span[0] != '/')
            return null;

        var spaceIndex = span.IndexOf(' ');
        var token = spaceIndex >= 0 ? span[..spaceIndex] : span;
        var mentionIndex = token.IndexOf('@');
        if (mentionIndex >= 0)
            token = token[..mentionIndex];

        return token.TrimStart('/').ToString().ToLowerInvariant();
    }

    private static bool HasArgs(string text)
    {
        var span = text.AsSpan().Trim();
        var spaceIndex = span.IndexOf(' ');
        return spaceIndex >= 0 && spaceIndex < span.Length - 1;
    }

    private static string CallbackPrefix(string data)
    {
        if (string.IsNullOrWhiteSpace(data))
            return "empty";

        var separators = new[] { ':', '|', ';', ' ' };
        var index = data.IndexOfAny(separators);
        var prefix = index >= 0 ? data[..index] : data;
        return prefix.Length <= 48 ? prefix : prefix[..48];
    }
}
