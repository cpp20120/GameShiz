using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record DeleteMessageEffect(
    ChatId ChatId,
    int MessageId,
    ModerationCaseId? CaseId,
    string CorrelationId,
    string CausationId,
    UserId? TargetUserId = null) : ModerationEffect;
