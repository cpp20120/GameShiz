namespace ChatAdministration.Domain.Effects;

public sealed record InlineKeyboardSpec(
    IReadOnlyList<IReadOnlyList<InlineKeyboardButtonSpec>> Rows);
