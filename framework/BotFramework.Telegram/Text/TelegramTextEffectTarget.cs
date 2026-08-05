using System.Globalization;
using BotFramework.Text;

namespace BotFramework.Telegram.Text;

internal static class TelegramTextEffectTarget
{
    public static long GetChatId(TextProcessingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.TryGetProperty<long>(TextProcessingKeys.ChatId, out var chatId))
            return chatId;

        if (context.TryGetProperty<int>(TextProcessingKeys.ChatId, out var intChatId))
            return intChatId;

        if (context.TryGetProperty<string>(TextProcessingKeys.ChatId, out var textChatId)
            && long.TryParse(textChatId, NumberStyles.Integer, CultureInfo.InvariantCulture, out chatId))
        {
            return chatId;
        }

        throw new InvalidOperationException(
            $"Telegram text effect requires '{TextProcessingKeys.ChatId}' in the processing context.");
    }

    public static int GetMessageId(string? effectMessageId, TextProcessingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = string.IsNullOrWhiteSpace(effectMessageId)
            ? context.MessageId
            : effectMessageId;
        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var messageId))
            return messageId;

        throw new InvalidOperationException(
            "Telegram text effect requires a numeric message identifier either on the effect or in the processing context.");
    }
}
