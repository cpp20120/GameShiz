using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Commands;

public sealed record UpdateLifecycleSettingsCommand(
    string CommandId,
    string IdempotencyKey,
    string CorrelationId,
    ChatId ChatId,
    UserId ActorUserId,
    string? WelcomeTemplate,
    string? GoodbyeTemplate,
    string? RulesText,
    bool? WelcomeEnabled,
    bool? GoodbyeEnabled,
    DateTimeOffset CreatedAt,
    ChatMemberRole ActorObservedRole,
    string ActorDisplayName);
