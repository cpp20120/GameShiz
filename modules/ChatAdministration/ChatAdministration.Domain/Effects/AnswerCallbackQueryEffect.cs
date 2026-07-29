namespace ChatAdministration.Domain.Effects;

public sealed record AnswerCallbackQueryEffect(
    string CallbackQueryId,
    string? Text = null,
    bool ShowAlert = false) : ModerationEffect;
