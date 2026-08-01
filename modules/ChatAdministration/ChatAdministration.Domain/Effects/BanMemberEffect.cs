using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record BanMemberEffect(
    ChatId ChatId,
    UserId UserId,
    DateTimeOffset? Until,
    ModerationCaseId? CaseId,
    string CorrelationId,
    string CausationId) : IModerationEffect;
