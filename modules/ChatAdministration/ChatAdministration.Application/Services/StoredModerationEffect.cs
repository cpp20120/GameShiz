using ChatAdministration.Domain.Effects;
using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record StoredModerationEffect(
    EffectId EffectId,
    string EffectType,
    ModerationEffect Payload,
    ModerationCaseId? CaseId,
    EffectImportance Importance,
    int Attempt,
    int MaximumAttempts);
