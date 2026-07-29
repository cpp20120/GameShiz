using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record RevokeModerationCaseCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    string CausationId,
    ChatId ChatId,
    UserId ActorUserId,
    ModerationCaseId CaseId,
    int? SourceMessageId,
    DateTimeOffset CreatedAt);
