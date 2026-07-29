using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record KickMemberEffect(
    ChatId ChatId,
    UserId UserId,
    ModerationCaseId? CaseId,
    string CorrelationId,
    string CausationId) : ModerationEffect;
