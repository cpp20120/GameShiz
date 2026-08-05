using BotFramework.Text;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace BotFramework.Telegram.Text;

/// <summary>
/// Executes <see cref="AddReactionEffect"/> by setting the bot-owned Telegram reaction set
/// to the requested emoji. Use <see cref="SetMessageReactionsEffect"/> when replacing or clearing
/// the complete reaction set is semantically important.
/// </summary>
public sealed class TelegramAddReactionEffectHandler(ITelegramBotClient bot)
    : MessageEffectHandler<AddReactionEffect>
{
    private readonly ITelegramBotClient _bot = bot ?? throw new ArgumentNullException(nameof(bot));

    protected override async ValueTask ExecuteAsync(
        AddReactionEffect effect,
        TextProcessingContext context,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(effect.Reaction);

        var chatId = TelegramTextEffectTarget.GetChatId(context);
        var messageId = TelegramTextEffectTarget.GetMessageId(effect.MessageId, context);
        ReactionType[] reactions = [new ReactionTypeEmoji { Emoji = effect.Reaction }];
        await _bot.SetMessageReaction(chatId, messageId, reactions, cancellationToken: cancellationToken);
    }
}
