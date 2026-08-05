using BotFramework.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace BotFramework.Telegram.Text;

/// <summary>Replaces or clears the bot-owned reaction set for a Telegram message.</summary>
public sealed class TelegramSetMessageReactionsEffectHandler(ITelegramBotClient bot)
    : MessageEffectHandler<SetMessageReactionsEffect>
{
    private readonly ITelegramBotClient _bot = bot ?? throw new ArgumentNullException(nameof(bot));

    protected override async ValueTask ExecuteAsync(
        SetMessageReactionsEffect effect,
        TextProcessingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(effect.Reactions);

        var chatId = TelegramTextEffectTarget.GetChatId(context);
        var messageId = TelegramTextEffectTarget.GetMessageId(effect.MessageId, context);
        if (effect.Reactions.Count > 1)
        {
            throw new InvalidOperationException(
                "Telegram bots can set at most one non-paid reaction per message.");
        }

        var reactions = effect.Reactions
            .Select(static emoji =>
            {
                ArgumentException.ThrowIfNullOrWhiteSpace(emoji);
                return (ReactionType)new ReactionTypeEmoji { Emoji = emoji };
            })
            .ToArray();

        await _bot.SetMessageReaction(
            chatId,
            messageId,
            reactions,
            effect.IsBig,
            cancellationToken);
    }
}
