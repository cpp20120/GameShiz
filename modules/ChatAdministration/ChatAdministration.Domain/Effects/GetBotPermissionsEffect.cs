using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record GetBotPermissionsEffect(
    ChatId ChatId,
    UserId BotUserId,
    string ObservationKey,
    string CorrelationId,
    string CausationId) : IModerationEffect;
