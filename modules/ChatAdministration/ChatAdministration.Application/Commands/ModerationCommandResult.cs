using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Commands;

public sealed record ModerationCommandResult(
    bool Accepted,
    bool Duplicate,
    string? ErrorCode,
    ModerationCaseId? CaseId,
    string ResponseText);
