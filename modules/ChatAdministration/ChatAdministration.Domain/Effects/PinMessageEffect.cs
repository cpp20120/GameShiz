using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record PinMessageEffect(
    ChatId ChatId,
    int MessageId,
    bool DisableNotification,
    string CorrelationId,
    string CausationId) : ModerationEffect;
