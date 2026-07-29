namespace ChatAdministration.Application.Parsing;

public sealed record ParsedManualModeration(TimeSpan? Duration, string? Reason);
