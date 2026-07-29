using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record ModerationCaseQuery(
    ChatId ChatId,
    UserId ActorUserId,
    UserId? TargetUserId,
    ChatMemberRole ActorObservedRole,
    ChatMemberRole TargetObservedRole,
    string ActorDisplayName,
    string TargetDisplayName,
    int Limit);
