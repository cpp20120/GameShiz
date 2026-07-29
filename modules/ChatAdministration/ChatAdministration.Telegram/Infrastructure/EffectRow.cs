namespace ChatAdministration.Telegram.Infrastructure;

internal sealed record EffectRow(
    Guid EffectId,
    string EffectType,
    string PayloadJson,
    Guid? CaseId,
    string Importance,
    int Attempt,
    int MaximumAttempts);
