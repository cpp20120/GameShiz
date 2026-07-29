using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Commands;

public sealed record OpenAppealCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    string CausationId,
    ChatId ChatId,
    UserId AuthorUserId,
    ModerationCaseId CaseId,
    string Text,
    int? SourceMessageId,
    DateTimeOffset CreatedAt);
