using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record ResolvedTarget(UserId UserId, string? Username, string DisplayName);
