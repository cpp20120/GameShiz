using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record WarningListResult(
    bool Accepted,
    string? ErrorCode,
    IReadOnlyList<WarningState> Warnings,
    string ResponseText);
