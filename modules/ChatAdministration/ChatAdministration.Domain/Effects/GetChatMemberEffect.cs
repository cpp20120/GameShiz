using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record GetChatMemberEffect(
    ChatId ChatId,
    UserId UserId,
    string ObservationKey,
    string CorrelationId,
    string CausationId) : IModerationEffect;
