using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record CreateModerationCaseEffect(
    ModerationCaseState Case,
    string CorrelationId,
    string CausationId) : ModerationEffect;
