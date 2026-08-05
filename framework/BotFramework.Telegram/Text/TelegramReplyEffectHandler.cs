using BotFramework.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace BotFramework.Telegram.Text;

/// <summary>Executes the platform-neutral <see cref="ReplyEffect"/> through Telegram.</summary>
public sealed class TelegramReplyEffectHandler(ITelegramBotClient bot)
    : MessageEffectHandler<ReplyEffect>
{
    private readonly ITelegramBotClient _bot = bot ?? throw new ArgumentNullException(nameof(bot));

    protected override async ValueTask ExecuteAsync(
        ReplyEffect effect,
        TextProcessingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effect.Text);

        var chatId = TelegramTextEffectTarget.GetChatId(context);
        var replyToMessageId = TelegramTextEffectTarget.GetMessageId(effect.ReplyToMessageId, context);
        await _bot.SendMessage(
            chatId,
            effect.Text,
            replyParameters: new ReplyParameters { MessageId = replyToMessageId },
            cancellationToken: cancellationToken);
    }
}
