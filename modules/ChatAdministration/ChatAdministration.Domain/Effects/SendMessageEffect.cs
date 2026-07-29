using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record SendMessageEffect(
    ChatId ChatId,
    string Text,
    int? ReplyToMessageId = null,
    MessageParseMode ParseMode = MessageParseMode.Plain,
    InlineKeyboardSpec? InlineKeyboard = null) : ModerationEffect;
