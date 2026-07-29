using ChatAdministration.Domain.Models;
using ChatAdministration.Domain.Policies;

namespace ChatAdministration.Application.Services;

public sealed record WarningMutationCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    string CausationId,
    ChatId ChatId,
    UserId ActorUserId,
    UserId TargetUserId,
    IReadOnlyCollection<WarningState> Warnings,
    IReadOnlyCollection<DomainEvent> Events,
    string ResponseText,
    DateTimeOffset CreatedAt,
    int? SourceMessageId);
