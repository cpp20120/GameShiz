using ChatAdministration.Domain.Models;

namespace ChatAdministration.Domain.Policies;

public sealed record MuteRequest(
    ChatId ChatId,
    UserId ActorUserId,
    UserId TargetUserId,
    TimeSpan Duration,
    string? Reason,
    string CorrelationId,
    string CausationId,
    DateTimeOffset Now,
    int? SourceMessageId = null);
