using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record CaseListResult(
    bool Accepted,
    string? ErrorCode,
    IReadOnlyList<ModerationCaseState> Cases,
    string ResponseText);
