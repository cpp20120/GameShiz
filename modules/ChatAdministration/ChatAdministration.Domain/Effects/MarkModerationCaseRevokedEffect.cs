using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record MarkModerationCaseRevokedEffect(
    ModerationCaseId CaseId,
    string CorrelationId,
    string CausationId) : IModerationEffect;
