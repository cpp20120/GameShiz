namespace ChatAdministration.Application.Parsing;

public sealed record ParsedMute(TimeSpan Duration, string? Reason);
