using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Effects;

public sealed record UnpinMessageEffect(
    ChatId ChatId,
    int MessageId,
    string CorrelationId,
    string CausationId) : ModerationEffect;
