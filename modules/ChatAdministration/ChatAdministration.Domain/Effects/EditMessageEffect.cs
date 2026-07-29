using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record EditMessageEffect(
    ChatId ChatId,
    int MessageId,
    string Text,
    MessageParseMode ParseMode,
    InlineKeyboardSpec? InlineKeyboard,
    string CorrelationId,
    string CausationId) : ModerationEffect;
