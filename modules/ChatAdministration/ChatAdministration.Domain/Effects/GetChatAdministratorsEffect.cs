using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record GetChatAdministratorsEffect(
    ChatId ChatId,
    string ObservationKey,
    string CorrelationId,
    string CausationId) : IModerationEffect;
