using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record UnrestrictMemberEffect(
    ChatId ChatId,
    UserId UserId,
    ModerationCaseId? CaseId,
    DateTimeOffset? ExpectedUntil,
    string CorrelationId,
    string CausationId) : IModerationEffect;
