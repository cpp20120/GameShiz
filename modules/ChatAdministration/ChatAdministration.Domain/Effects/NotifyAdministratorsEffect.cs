using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record NotifyAdministratorsEffect(
    ChatId ChatId,
    string Text,
    string CorrelationId,
    string CausationId) : ModerationEffect;
