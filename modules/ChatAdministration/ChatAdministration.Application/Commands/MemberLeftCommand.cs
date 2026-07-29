using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Commands;

public sealed record MemberLeftCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    string CausationId,
    ChatId ChatId,
    UserId UserId,
    string DisplayName,
    string? Username,
    DateTimeOffset CreatedAt);
