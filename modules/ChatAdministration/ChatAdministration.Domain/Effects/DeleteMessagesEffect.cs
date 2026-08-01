using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record DeleteMessagesEffect(
    ChatId ChatId,
    IReadOnlyList<int> MessageIds,
    ModerationCaseId? CaseId,
    string CorrelationId,
    string CausationId) : IModerationEffect;
