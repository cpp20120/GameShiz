using BotFramework.Text;
using Telegram.Bot;

namespace BotFramework.Telegram.Text;

/// <summary>Executes the platform-neutral <see cref="DeleteMessageEffect"/> through Telegram.</summary>
public sealed class TelegramDeleteMessageEffectHandler(ITelegramBotClient bot)
    : MessageEffectHandler<DeleteMessageEffect>
{
    private readonly ITelegramBotClient _bot = bot ?? throw new ArgumentNullException(nameof(bot));

    protected override async ValueTask ExecuteAsync(
        DeleteMessageEffect effect,
        TextProcessingContext context,
        CancellationToken cancellationToken)
    {
        var chatId = TelegramTextEffectTarget.GetChatId(context);
        var messageId = TelegramTextEffectTarget.GetMessageId(effect.MessageId, context);
        await _bot.DeleteMessage(chatId, messageId, cancellationToken);
    }
}
