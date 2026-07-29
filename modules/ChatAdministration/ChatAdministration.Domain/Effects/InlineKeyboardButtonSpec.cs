namespace ChatAdministration.Domain.Effects;

public sealed record InlineKeyboardButtonSpec(
    string Text,
    string CallbackData);
