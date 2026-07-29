using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record RestrictMemberEffect(
    ChatId ChatId,
    UserId UserId,
    DateTimeOffset Until,
    ModerationCaseId? CaseId,
    string CorrelationId,
    string CausationId) : ModerationEffect;
