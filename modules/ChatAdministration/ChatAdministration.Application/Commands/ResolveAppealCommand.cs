using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Commands;

public sealed record ResolveAppealCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    string CausationId,
    ChatId ChatId,
    UserId ActorUserId,
    AppealId AppealId,
    bool Approve,
    string? ResolutionComment,
    int? SourceMessageId,
    DateTimeOffset CreatedAt);
