using ChatAdministration.Domain.Models;

namespace ChatAdministration.Application.Services;

public sealed record PersistCommandResult(bool Duplicate, ModerationCaseId? CaseId);
