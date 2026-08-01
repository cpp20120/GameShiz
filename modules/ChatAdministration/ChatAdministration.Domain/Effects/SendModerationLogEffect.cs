using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record SendModerationLogEffect(
    ChatId ChatId,
    string Text,
    string CorrelationId,
    string CausationId) : IModerationEffect;
