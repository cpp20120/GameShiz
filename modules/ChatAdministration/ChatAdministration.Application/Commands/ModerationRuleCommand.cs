using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Commands;

public sealed record ModerationRuleCommand(
    string CommandId,
    string CorrelationId,
    ChatId ChatId,
    UserId ActorUserId,
    ChatMemberRole ActorObservedRole,
    RuleId RuleId,
    bool Enabled,
    int? ScoreOverride,
    DateTimeOffset CreatedAt);
