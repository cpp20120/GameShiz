using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record ModerationContext(
    ChatState Chat,
    MemberState Actor,
    MemberState Target);
