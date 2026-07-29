using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record UpdateModerationCaseEffect(
    ModerationCaseId CaseId,
    ModerationCaseStatus Status,
    string Reason,
    string CorrelationId,
    string CausationId) : ModerationEffect;
